namespace VSharp.Interpreter

open VSharp
open VSharp.Core
open System.Collections
open System.Collections.Generic
open System.Reflection
open Logger

module public SVM =

    let private init = lazy(Core.API.Configure (new Activator()) (new SymbolicInterpreter()))

    let private atomizeSubtypeConstraint (summary : functionSummary) =
        summary.state |> API.Memory.SubtypeConstraint |> API.Memory.AtomizeConstraint |> Seq.map Term

    let private prepareAndInvoke (dictionary : IDictionary option) (m : MethodInfo) invoke =
        init.Force()
        let assemblyPath = JetBrains.Util.FileSystemPath.Parse m.Module.FullyQualifiedName
        let qualifiedTypeName = m.DeclaringType.AssemblyQualifiedName
        match DecompilerServices.methodInfoToMetadataMethod assemblyPath qualifiedTypeName m with
        | None ->
            printLog Warning "WARNING: metadata method for %s.%s not found!" qualifiedTypeName m.Name
            None
        | Some metadataMethod ->
            dictionary |> Option.iter (fun dictionary -> dictionary.Add(m, null))
            let summary = invoke ({ metadataMethod = metadataMethod; state = {v = Memory.EmptyState}}) id
            printfn
                "*********************************************\n%O\n*************************************************\n"
                (Seq.map toString (atomizeSubtypeConstraint summary) |> String.concat "\n")
            dictionary |> Option.iter (fun dictionary -> dictionary.[m] <- summary)
            Some summary
//            System.Console.WriteLine("For {0}.{1} got {2}!", m.DeclaringType.Name, m.Name, ControlFlow.ResultToTerm result)

    let private interpretEntryPoint (dictionary : IDictionary) (m : MethodInfo) =
        assert(m.IsStatic)
        prepareAndInvoke (Some dictionary) m InterpretEntryPoint |> ignore

    let private explore (dictionary : IDictionary) (m : MethodInfo) =
        prepareAndInvoke (Some dictionary) m Explore |> ignore

    let private exploreType ignoreList whiteList ep dictionary (t : System.Type) =
        let (|||) = Microsoft.FSharp.Core.Operators.(|||)
        let bindingFlags = BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly
        if List.forall (fun keyword ->
            not(t.AssemblyQualifiedName.Contains(keyword))) ignoreList &&
           (List.isEmpty whiteList || List.exists (fun keyword -> t.AssemblyQualifiedName.Contains(keyword)) whiteList) && t.IsPublic then
            t.GetMethods(bindingFlags)
            |> FSharp.Collections.Array.iter (fun m -> if m <> ep && not m.IsAbstract then
                                              try explore dictionary m with | _ -> ())

    let private replaceLambdaLines str =
        System.Text.RegularExpressions.Regex.Replace(str, @"@\d+(\+|\-)\d*\[Microsoft.FSharp.Core.Unit\]", "")

    let private resultToString (summary : functionSummary) =
        sprintf "%O\nHEAP:\n%s" summary.result (summary.state |> Memory.Dump |> replaceLambdaLines)

    let private subtypeResultToString (summary : functionSummary) =
        sprintf "\nTYPE CONSTRAINT:\n%s" (summary.state |> API.Memory.SubtypeConstraint |> List.map (toString) |> String.concat "\n")

    let private subtypeConstraint (summary : functionSummary) =
        summary.state |> API.Memory.SubtypeConstraint |> Seq.map (toString)

    let public ExploreOne (m : MethodInfo) =
        prepareAndInvoke None m Explore |> Option.get |> resultToString

    let public ConfigureSolver (solver : ISolver) =
        Core.API.ConfigureSolver solver

    let public Run (assembly : Assembly) (ignoreList : List<_>) (whiteList : List<_>) =
        let ignoreList = List.ofSeq ignoreList
        let whiteList = List.ofSeq whiteList
        let dictionary = new Dictionary<MethodInfo, functionSummary>()
        let ep = assembly.EntryPoint
        assembly.GetTypes() |> FSharp.Collections.Array.iter (exploreType ignoreList whiteList ep dictionary)
        if ep <> null then interpretEntryPoint dictionary ep
        System.Linq.Enumerable.ToDictionary(dictionary :> IEnumerable<_>, (fun kvp -> kvp.Key), fun (kvp : KeyValuePair<MethodInfo, functionSummary>) -> resultToString kvp.Value) :> IDictionary<_, _>

    let public RunGenerator (assembly : Assembly) (ignoreList : List<_>) (whiteList : List<_>) =
        let ignoreList = List.ofSeq ignoreList
        let whiteList = List.ofSeq whiteList
        let dictionary = new Dictionary<MethodInfo, functionSummary>()
        let ep = assembly.EntryPoint
        assembly.GetTypes() |> FSharp.Collections.Array.iter (exploreType ignoreList whiteList ep dictionary)
        if ep <> null then interpretEntryPoint dictionary ep
        System.Linq.Enumerable.ToDictionary(dictionary :> IEnumerable<_>, (fun kvp -> kvp.Key), fun (kvp : KeyValuePair<MethodInfo, functionSummary>) -> atomizeSubtypeConstraint kvp.Value) :> IDictionary<_, _>
