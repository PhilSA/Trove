﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PolymorphicElementsSourceGenerators
{
    public class FileWriter
    {
        public string FileContents = "";

        private int _indentLevel = 0;

        public void WriteLine(string line)
        {
            FileContents += GetIndentString() + line + "\n";
        }

        public void WriteUsingsAndRemoveDuplicates(List<string> usings)
        {
            // Remove duplicates
            List<string> filteredUsings = new List<string>();
            foreach (var item in usings)
            {
                if (string.IsNullOrEmpty(item) || string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                if (filteredUsings.Contains(item))
                {
                    continue;
                }

                filteredUsings.Add(item);
            }

            foreach (var item in filteredUsings)
            {
                WriteLine($"using {item};");
            }
        }

        public void WriteInScope(System.Action writeAction, string afterClosingBracket = "")
        {
            WriteLine("{");
            _indentLevel++;

            writeAction.Invoke();

            _indentLevel--;
            WriteLine("}" + afterClosingBracket);
        }

        public void WriteInNamespace(string namespaceName, System.Action writeAction)
        {
            if (!string.IsNullOrEmpty(namespaceName))
            {
                WriteLine("namespace " + namespaceName);
                WriteInScope(writeAction);
            }
            else
            {
                writeAction.Invoke();
            }
        }

        private string GetIndentString()
        {
            string indentation = "";
            for (int i = 0; i < _indentLevel; i++)
            {
                indentation += "\t";
            }
            return indentation;
        }
    }
}