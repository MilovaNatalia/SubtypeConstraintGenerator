using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.AccessControl;
using System.Threading;
using VSharp.Interpreter;
using VSharp.Core;


namespace TypeConstraintGenerator
{
    public class DumpStackTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            Console.Write(message);
        }

        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public override void Fail(string message)
        {
            Fail(message, String.Empty);
        }

        public override void Fail(string message1, string message2)
        {
            Console.WriteLine("ASSERT FAILED");
            Console.WriteLine("{0}: {1}", message1, message2);
            Console.WriteLine("Stack Trace:");

            StackTrace trace = new StackTrace( true );
            StackFrame[] stackFrames = trace.GetFrames();
            if (stackFrames != null)
            {
                foreach (StackFrame frame in stackFrames)
                {
                    MethodBase frameClass = frame.GetMethod();
                    Console.WriteLine("  {2}.{3} {0}:{1}",
                        frame.GetFileName(),
                        frame.GetFileLineNumber(),
                        frameClass.DeclaringType,
                        frameClass.Name);
                }
            }
        }
    }

    public sealed class Generator
    {
        private const string TestsDirectoryName = "Tests";

        private static string _currentTestDirectory = "";

        private static Assembly LoadFromTestFolder(object sender, ResolveEventArgs args)
        {
            // This handler is called only when the common language runtime tries to bind to the assembly and fails.
            string name = new AssemblyName(args.Name).Name;
            string additionalPath = _currentTestDirectory + Path.DirectorySeparatorChar + name + ".dll";
            if (File.Exists(additionalPath))
            {
                return Assembly.LoadFrom(additionalPath);
            }

            return null;
        }

        public static List<Tuple<IEnumerable<Tuple<object, object>>, IEnumerable<Tuple<object, object>>>> Generate()
        {
            Trace.Listeners.Add(new DumpStackTraceListener());

            CultureInfo ci = new CultureInfo("en-GB");
            ci.NumberFormat.PositiveInfinitySymbol = "Infinity";
            ci.NumberFormat.NegativeInfinitySymbol = "-Infinity";
            Thread.CurrentThread.CurrentCulture = ci;

            var ignoredLibs = new List<string>
            {
            };

            var ignoredTypes = new List<string>
            {
            };

            var whiteTypes = new List<string>
            {
            };
            
            var result = new List<Tuple<IEnumerable<Tuple<object, object>>, IEnumerable<Tuple<object, object>>>>();

            string pathToTests = Path.Combine(Path.GetFullPath("."), "..", "..", "..", TestsDirectoryName);
            string[] tests = Directory.GetDirectories(pathToTests);
            foreach (string testDir in tests)
            {
                string[] libEntries = Directory.GetFiles(testDir);
                foreach (string lib in libEntries)
                {
                    if (!lib.EndsWith(".dll", StringComparison.Ordinal) || ignoredLibs.Exists(i => lib.EndsWith(i)))
                    {
                        continue;
                    }

                    _currentTestDirectory = testDir;
                    AppDomain currentDomain = AppDomain.CurrentDomain;
                    currentDomain.AssemblyResolve += LoadFromTestFolder;

                    IDictionary<MethodInfo, IEnumerable<API.Term>> got = SVM.RunGenerator(Assembly.LoadFile(lib), ignoredTypes, whiteTypes);

                    
                    foreach (var constr in got.Values)
                    {
                        var positives = new List<Tuple<object, object>>();
                        var negatives = new List<Tuple<object, object>>();
                        
                        foreach (var str in constr)
                        {
                            var negation = false;
                            termNode.Constant c;
                            if (str.Term.term.IsConstant)
                            {
                                c = (termNode.Constant) str.Term.term;
                            }
                            else
                            {
                                c = (termNode.Constant) ((termNode.Expression) str.Term.term).Item2[0].term;
                                negation = true;
                            }

                            var subtypeSource = (Common.symbolicSubtypeSource) c.Item2;

                            object left;
                            object right;

                            if (subtypeSource.left.IsTypeVariable)
                                if (((termType.TypeVariable)subtypeSource.left).Item.IsImplicit)
                                    left = subtypeSource.left.ToString();
                                else
                                {
                                    left = Types.toDotNetType(subtypeSource.left);
                                }
                            else
                            {
                                left = Types.toDotNetType(subtypeSource.left);
                            }

                            if (subtypeSource.right.IsTypeVariable)
                                if (((termType.TypeVariable)subtypeSource.right).Item.IsImplicit)
                                    right = subtypeSource.right.ToString();
                                else
                                {
                                    right = Types.toDotNetType(subtypeSource.right);
                                }
                            else
                            {
                                right = Types.toDotNetType(subtypeSource.right);
                            }
                            
                            if (negation)
                                negatives.Add(new Tuple<object, object>(left, right));
                            else
                            {
                                positives.Add(new Tuple<object, object>(left, right));   
                            }
                        }

                        result.Add(
                            new Tuple<IEnumerable<Tuple<object, object>>, IEnumerable<Tuple<object, object>>>(positives,
                                new List<Tuple<object, object>>()));
                        result.Add(
                            new Tuple<IEnumerable<Tuple<object, object>>, IEnumerable<Tuple<object, object>>>(new List<Tuple<object, object>>(),
                                negatives));
                    }
                    
                }
            }

            return result;
        }
    }

    internal static class Program
    {
        public static void Main(string[] args)
        {
            Generator.Generate();
        }
    }
}