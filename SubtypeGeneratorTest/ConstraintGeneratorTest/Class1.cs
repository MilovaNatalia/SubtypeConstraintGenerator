using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SubtypeGeneratorTest
{
    public interface IA<out X> {}
    public interface IB<in X> {}
    public interface IC<X> {}
    public interface ID<out X, out  Y> {}
    public interface IE {}
    public interface IF {}
    public interface IG {}
    public interface IH {}
    public interface II {}
    public interface IK {}
    public interface IL {}
    public interface IM {}
    public interface IN {}
    public interface IO {}
    public interface IP {}
    public interface IQ {}
    public interface IR {}
    public interface IS {}
    public interface IT {}
    public interface IV {}
    public interface IX {}
    public interface IY {}
    public interface IZ {}
    public class A {}
    public class B : A, IE, IF, IK {}
    public class C : B {}
    public class D : C, IL, IM, IQ {}
    public class E  : C, IG, IH, IQ {}
    public class H : D, IA<K> {}
    public class I : D , IA<L> {}
    public class K : E {}
    public class L : E {}

    public class F : IA<F> {}

    public class G<X> : IA<X> {}
    
    public class M : IC<B>{}
    public class N : IC<K>{}
    public class O : ID<O, O> {}
    public class P : IB<Object> {}
    public class Q<C, V, U, N>
        where C : class
        where V : struct
        where U : unmanaged
        where N : new()
    {}
    public class R<A, B>
        where A : IA<B>
        where B : IA<A>
    {}
    public class S {}
    public class T {}
    public class V {}
    public class X {}
    public class Y {}
    public class Z {}

    public struct SA {}
    public struct SB {}
    public struct SC {}
    public struct SD {}
    public struct SE {}
    
    public interface IBox<in Z>
    {
    }

    public class Thing : IBox<IBox<Thing>>
    {
    }

    public class ClassA<T> where T : IEnumerable<T>{}
    
    public class Mammals {}  
    public class Dogs : Mammals {}  
    
    public delegate Dogs DogsDelegate<T, U>(T t1, U t2);
    
    public static class Tests
    {
        private static int number;

//        public static void InvariantTypes<X, Y>(X x, Y y)
//        {
//            if (x is Array && y is Action)
//            {
//                number++;
//            }
//        }
//
//        public static void TuplesTypes1<X>(X x)
//        {
//            if (x is Tuple<Tuple<X>> && x is Tuple<X>)
//            {
//                number++;
//            }
//        }
//
//        public static void TuplesTypes2<X, Y>(X x, Y y)
//        {
//            if (x is Tuple<X, Y> && x is Tuple<Tuple<X>, X>)
//            {
//                number++;
//            }
//        }
//
//
//        public static void ArrayTypes<X>(int[,] arr)
//        {
//            if ( arr is X )
//            {
//                number++;
//            }
//        }
//
//        public static void ExpressionTypes<X>(Expression expr)
//        {
//            if (expr is IQueryable<X>)
//            {
//                number++;
//            }
//        }
//
//        public static void InfiniteTypes (Thing thing)
//        {
//            if (thing is IBox<Thing>)
//            {
//                number++;
//            }
//        }
        
//        public static void InvariantType(M m, N n)
//        {
//            var m1 = (Object) m;
//            var n1 = (Object) n;
//            if (n1 is M && m1 is N)
//            {
//                number++;
//            }
//        }
//
//        public static void InvariantType<T, K>(T m, K n)
//        {
//            var m1 = (Object) m;
//            var n1 = (Object) n;
//            if (n1 is M && m1 is N)
//            {
//                number++;
//            }
//        }
//
//        public static void VariantGenericType<T, K>(IA<T> ia, K k)
//        {
//            if (ia is K || k is IA<K>)
//            {
//                number++;
//            }
//        }
//
//        public static void GenericType<L>(H h, I i, IA<H> iah)
//        {
//            if (h is IA<L> && i is IA<H> || i is IA<L> && iah is IA<I>)
//            {
//                var h1 = (IA<IQ>)h;
//                var i1 = (IA<IG>)i;
//                number++;
//            }
//        }
//
//        public static void StructType<K, T>(T k)
//            where K : struct
//        {
//            if (k is IA<K> || k is int || k is SA && k is SB || k is SC && k is IK)
//            {
//                number++;
//            }
//        }
//
/*        public static void ArrayType<A>(A[] aa, Object[] oa, int k)
        {
            if (oa is A[] && oa[k] is IH || oa[5] is A)
            {
                number++;
            }
        }*/

        public static void Array2Type<A>(A[,] aa, Object[,] oa, int k)
        {
            if (oa is A[,] && oa[k,k] is IH || oa[5,6] is A)
            {
                number++;
            }
        }

        public static void SystemArrayType<A>(A[] aa, Object[,] oa, int k, Array array)
        {
            if (array is A[] ||
                oa[5,6] is A && array is int[,])
            {
                number++;
            }
        }

//        public static void ConstraintType<C, V, U, N, A, B, D>(
//            C c, U u, N n, A a, B b, D d)
//            where C : class
//            where U : unmanaged
//            where N : new()
//            where A : IA<B>
//            where B : IA<A>
//        {
//            if (c is V || u is N || n is V || d is V && d is A || a is IA<A> && b is IA<B>)
//            {
//                number++;
//            }
//        }

        public static void DelegateFuncType<T, U, K, M>(M ftu, Func<U,K,M> fukm, Func<T,K,M> ftkm)
        {
            if (ftu is Func<T, Dictionary<T,U>> || ftkm is Func<T, Func<T,U>, T> )
            {
                number++;
            }
        }
        
        public static void DelegateType<T, U, K, M>(U u, K k)
        {
            if (u is DogsDelegate<T,M>)
            {
                number++;
            }
        }

        public static void ArrayTest<T>()
        {
            var array = new T[6];
            if (array is T)
            {
                number++;
            }
        }
    }
}