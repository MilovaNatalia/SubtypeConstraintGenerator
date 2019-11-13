﻿namespace VSharp.Core

#nowarn "69"

open VSharp
open Core.Option
open VSharp.Core.Types.Constructor

type SolverResult = Sat | Unsat | Unknown
type ISolver =
    abstract Solve : term -> SolverResult
    abstract SolvePathCondition : term -> term list -> SolverResult

module public Common =
    let mutable private solver : ISolver option = None
    let configureSolver s = solver <- Some s
    let private solve term =
        match solver with
        | Some s -> s.Solve term
        | None -> Unknown
    let private solvePC term pc =
        match solver with
        | Some s -> s.SolvePathCondition term pc
        | None -> Unknown

// ------------------------------- Simplification -------------------------------

    let simplifyPairwiseCombinations = Propositional.simplifyPairwiseCombinations

    let simplifyConcreteBinary simplify mtd isChecked t x y xval yval _ _ state =
        simplify (Metadata.combine3 mtd x.metadata y.metadata) isChecked state t xval yval

    let rec simplifyGenericUnary name state x matched concrete unmatched =
        match x.term with
        | Error _ -> matched (x, state)
        | Concrete(xval, typeofX) -> concrete x xval typeofX state |> matched
        | GuardedValues(guards, values) ->
            Cps.List.mapFoldk (fun state term matched -> simplifyGenericUnary name state term matched concrete unmatched) state values (fun (values', state) ->
                (Merging.merge (List.zip guards values'), state) |> matched)
        | _ -> unmatched x state matched

    let rec simplifyGenericBinary _ state x y matched concrete unmatched repeat =
        match x.term, y.term with
        | Error _, _ -> matched (x, state)
        | _, Error _ -> matched (y, state)
        | Concrete(xval, typeOfX), Concrete(yval, typeOfY) -> concrete x y xval yval typeOfX typeOfY state |> matched
        | Union(gvsx), Union(gvsy) ->
            let compose (gx, vx) state (gy, vy) matched = repeat vx vy state (fun (xy, state) -> ((gx &&& gy, xy), state) |> matched)
            let join state (gx, vx) k = Cps.List.mapFoldk (compose (gx, vx)) state gvsy k
            Cps.List.mapFoldk join state gvsx (fun (gvss, state) -> (Merging.merge (List.concat gvss), state) |> matched)
        | GuardedValues(guardsX, valuesX), _ ->
            Cps.List.mapFoldk (fun state x matched -> repeat x y state matched) state valuesX (fun (values', state) ->
            (Merging.merge (List.zip guardsX values'), state) |> matched)
        | _, GuardedValues(guardsY, valuesY) ->
            Cps.List.mapFoldk (fun state y matched -> repeat x y state matched) state valuesY (fun (values', state) ->
            (Merging.merge (List.zip guardsY values'), state) |> matched)
        | _ -> unmatched x y state matched

// ------------------------------- Type casting -------------------------------

    // TODO: support composition for this constant source
    [<StructuralEquality;NoComparison>]
    type public symbolicSubtypeSource =
        {left : termType; right : termType}
        interface IStatedSymbolicConstantSource with
            override x.SubTerms = Seq.empty

    let (|SubtypeOperation|_|) = function
        | Constant(_, source, _) when (source :? symbolicSubtypeSource) -> Some(SubtypeOperation)
        | _ -> None

    let subtypeConstraint (state : state) =
        let rec purifyConstraintk def (term : term) k =
            match term.term with
            | SubtypeOperation -> k term
            | Conjunction xs -> Cps.List.mapk (purifyConstraintk True) xs (conjunction term.metadata >> k)
            | Disjunction xs -> Cps.List.mapk (purifyConstraintk False) xs (disjunction term.metadata >> k)
            | Negation x -> purifyConstraintk (negate def def.metadata) x ((!!) >> k)
            | _ -> k def
        let purifyConstraint def term = purifyConstraintk def term id
        List.map (purifyConstraint True) state.traceConstraint |> Seq.distinct |> Seq.filter (fun t -> not <| (isTrue t || isFalse t)) |> List.ofSeq

    let atomizeConstraint (constr : term list) =
        let rec atomizek (term : term) k =
            match term.term with
            | Disjunction xs
            | Conjunction xs -> Cps.List.mapk atomizek xs (List.concat >> k)
            | Negation x -> atomizek x (List.map (!!) >> k)
            | _ -> k [term]
        let atomize term = atomizek term id
        List.map atomize constr |> List.concat |> Seq.distinct |> List.ofSeq

    let rec is metadata leftType rightType =
        let makeSubtypeBoolConst leftTermType rightTermType =
            let subtypeName = sprintf "(%O <: %O)" leftTermType rightTermType
            let source = {left = leftTermType; right = rightTermType}
            Constant metadata subtypeName source Bool

        let isGround t = TypeUtils.isGround(Types.toDotNetType t)

        match leftType, rightType with
        | _ when leftType = rightType -> makeTrue metadata
        | termType.Null, _
        | Void, _   | _, Void
        | Bottom, _ | _, Bottom -> makeFalse metadata
        | Reference _, Reference _ -> makeTrue metadata
        | Pointer _, Pointer _ -> makeTrue metadata
        | Func(_, largs, lret) , Func(_, rargs, rret) -> makeSubtypeBoolConst leftType rightType
        | ArrayType _ as t1, (ArrayType(_, SymbolicDimension name) as t2) ->
            if name.v = "System.Array" then makeTrue metadata else makeSubtypeBoolConst t1 t2
        | ArrayType(_, SymbolicDimension _) as t1, (ArrayType _ as t2)  when t1 <> t2 ->
            makeSubtypeBoolConst t1 t2
        | ArrayType(t1, ConcreteDimension d1), ArrayType(t2, ConcreteDimension d2) ->
            if d1 = d2 then is metadata t1 t2 else makeFalse metadata
        | TypeVariable(Implicit (_, _, t)) as t1, t2 ->
            is metadata t t2 ||| makeSubtypeBoolConst t1 t2
        | termType.InterfaceType(_, _), TypeVariable(Implicit (_, _, t)) when (not <| Types.isObject t) ->
            makeFalse metadata
        | t1, (TypeVariable(Implicit (_, _, t)) as t2) ->
            is metadata t1 t &&& makeSubtypeBoolConst t1 t2
        | ConcreteType lt as t1, (ConcreteType rt as t2) ->
            if rt.IsAssignableFrom lt then makeTrue metadata
            elif isGround t1 && isGround t2 then makeFalse metadata
            else makeSubtypeBoolConst t1 t2
        | _ -> makeFalse metadata

    let typesEqual mtd x y = is mtd x y &&& is mtd y x

    // TODO: support composition for this constant source
    [<StructuralEquality;NoComparison>]
    type private isValueTypeConstantSource =
        {termType : termType}
        interface IStatedSymbolicConstantSource with
            override x.SubTerms = Seq.empty

    let internal isValueType metadata termType =
        let makeIsValueTypeBoolConst termType =
            Constant metadata (sprintf "IsValueType(%O)" termType) ({termType = termType}) Bool
        match termType with
        | ConcreteType t when t.IsValueType -> makeTrue metadata
        | TypeVariable(Implicit(_, _, t)) ->
            if (Types.toDotNetType t).IsValueType
                then makeIsValueTypeBoolConst termType
                else makeFalse metadata
        | _ -> makeFalse metadata

    type public symbolicSubtypeSource with
        interface IStatedSymbolicConstantSource with
            override x.Compose ctx state =
                let left = State.substituteTypeVariables ctx state x.left
                let right = State.substituteTypeVariables ctx state x.right
                is ctx.mtd left right

    type isValueTypeConstantSource with
         interface IStatedSymbolicConstantSource with
            override x.Compose ctx state =
                let typ = State.substituteTypeVariables ctx state x.termType
                isValueType ctx.mtd typ

// ------------------------------- Branching -------------------------------

    let statelessConditionalExecution conditionInvocation thenBranch elseBranch merge merge2 k =
        let execution condition k =
            thenBranch (fun thenResult ->
            elseBranch (fun elseResult ->
            k <| merge2 condition !!condition thenResult elseResult))
        let chooseBranch condition k =
            match condition with
            | Terms.True ->  thenBranch k
            | Terms.False -> elseBranch k
            | condition ->
                match solve condition with
                | Unsat -> elseBranch k
                | _ ->
                    match solve (!!condition) with
                    | Unsat -> thenBranch k
                    | _ -> execution condition k

        conditionInvocation (fun condition ->
        Merging.commonGuardedErroredApplyk chooseBranch id condition merge k)

    let statedConditionalExecution (state : state) conditionInvocation thenBranch elseBranch merge merge2 errorHandler k =
        let execution conditionState condition k =
            thenBranch (State.withPathCondition conditionState condition) (fun (thenResult, thenState) ->
            elseBranch (State.withPathCondition conditionState !!condition) (fun (elseResult, elseState) ->
            let result = merge2 condition !!condition thenResult elseResult
            let state = Merging.merge2States condition !!condition (State.popPathCondition thenState) (State.popPathCondition elseState)
            k (result, state)))
        let chooseBranch conditionState condition k =
            let thenCondition =
                Propositional.conjunction condition.metadata (condition :: State.pathConditionOf conditionState)
                |> Merging.unguard |> Merging.merge
            let elseCondition =
                Propositional.conjunction condition.metadata (!!condition :: State.pathConditionOf conditionState)
                |> Merging.unguard |> Merging.merge
            match thenCondition, elseCondition with
            | False, _ -> elseBranch conditionState k
            | _, False -> thenBranch conditionState k
            | _ ->
                match solvePC condition (State.pathConditionOf conditionState) with
                | Unsat -> elseBranch conditionState k
                | _ ->
                    match solvePC !!condition (State.pathConditionOf conditionState) with
                    | Unsat -> thenBranch conditionState k
                    | _ -> execution conditionState condition k

        conditionInvocation state (fun (condition, conditionState) ->
        Merging.commonGuardedErroredStatedApplyk chooseBranch errorHandler conditionState condition merge k)
