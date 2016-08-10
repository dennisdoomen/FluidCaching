using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MergeCSharpFiles
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("Usage: MergeCSharpFiles.exe <sourcePath> <pattern> <outputPath>");
            }

            var namespaceImports = new List<string>();
            var result = new StringBuilder();

            foreach (string sourceFile in Directory.GetFiles(args[0], args[1]))
            {
                Console.WriteLine($"Processing file {sourceFile}");

                foreach (string line in File.ReadAllLines(sourceFile))
                {
                    if (line.Trim().StartsWith("using"))
                    {
                        namespaceImports.Add(line.Trim());
                    }
                    else
                    {
                        result.AppendLine(line);
                    }
                }

                result.AppendLine("");
            }

            Console.WriteLine($"Writing usings to {args[2]}");
            File.WriteAllLines(args[2], namespaceImports.Distinct());

            Console.WriteLine($"Writing result to {args[2]}");
            File.AppendAllText(args[2], result.ToString());
        }
    }
}