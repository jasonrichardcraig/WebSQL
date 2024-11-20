
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace DeploymentScriptGenerator
{
    internal class Program
    {
        static readonly HashSet<string> SystemAssemblies = new HashSet<string>
            {
                "System", "System.Core", "System.Data", "System.Xml", "System.Numerics", "System.Xml.Linq"
            };

        static void Main(string[] args)
        {
            string xmlPath = Path.Combine("..", "..", "..", "TestHarness", "TestHarness.csproj");

            if (!File.Exists(xmlPath))
            {
                Console.WriteLine($"Error: XML file '{xmlPath}' not found.");
                return;
            }

            var assemblyPaths = ParseHintPathsFromXml(xmlPath).ToList();
            if (!assemblyPaths.Any())
            {
                Console.WriteLine("No assembly paths found in the XML.");
                return;
            }

            var sortedAssemblies = OrderAssembliesByDependencies(assemblyPaths);
            GenerateSqlScripts(sortedAssemblies);

            Console.ReadLine();
        }

        static IEnumerable<string> ParseHintPathsFromXml(string xmlPath)
        {
            var doc = XDocument.Load(xmlPath);
            string defaultPath = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319";

            return doc.Descendants(XName.Get("Reference", "http://schemas.microsoft.com/developer/msbuild/2003"))
                      .Select(reference =>
                      {
                          var hintPathElement = reference.Element(XName.Get("HintPath", "http://schemas.microsoft.com/developer/msbuild/2003"));
                          if (hintPathElement != null)
                          {
                              return Path.GetFullPath(Path.Combine("..", "..", hintPathElement.Value));
                          }
                          else
                          {
                              string assemblyName = reference.Attribute("Include")?.Value.Split(',')[0] + ".dll";
                              string defaultAssemblyPath = Path.Combine(defaultPath, assemblyName);

                              return File.Exists(defaultAssemblyPath) ? defaultAssemblyPath : null;
                          }
                      })
                      .Where(path => path != null && File.Exists(path));
        }

        static List<string> OrderAssembliesByDependencies(List<string> assemblyPaths)
        {
            var dependencies = new Dictionary<string, List<string>>();
            var assemblyMap = assemblyPaths.ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => path);

            // Mapping from full assembly names to paths for resolving dependencies
            var fullNameAssemblyMap = assemblyPaths.ToDictionary(
                path => AssemblyName.GetAssemblyName(path).FullName,
                path => path,
                StringComparer.OrdinalIgnoreCase);

            // Event handler for resolving dependencies in reflection-only context
            ResolveEventHandler resolveEventHandler = (sender, args) =>
            {
                if (fullNameAssemblyMap.TryGetValue(args.Name, out var assemblyPath))
                {
                    return Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                }
                // Attempt to load from GAC or standard locations
                return Assembly.ReflectionOnlyLoad(args.Name);
            };

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolveEventHandler;

            foreach (var path in assemblyPaths)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(path);
                if (SystemAssemblies.Contains(assemblyName)) continue;

                try
                {
                    var assembly = Assembly.ReflectionOnlyLoadFrom(path);

                    var referencedNames = assembly.GetReferencedAssemblies()
                                                  .Select(a => a.Name)
                                                  .Where(name => assemblyMap.ContainsKey(name));

                    dependencies[assemblyName] = referencedNames.ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load assembly '{assemblyName}' for dependency analysis: {ex.Message}");
                }
            }

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolveEventHandler;

            return TopologicalSort(dependencies).Select(name => assemblyMap[name]).ToList();
        }

        static IEnumerable<string> TopologicalSort(Dictionary<string, List<string>> dependencies)
        {
            var sorted = new List<string>();
            var visited = new HashSet<string>();

            void Visit(string assembly)
            {
                if (visited.Contains(assembly)) return;

                visited.Add(assembly);

                if (dependencies.TryGetValue(assembly, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        Visit(dep);
                    }
                }

                sorted.Add(assembly);
            }

            foreach (var assembly in dependencies.Keys)
            {
                Visit(assembly);
            }

            return sorted;
        }

        static void GenerateSqlScripts(IEnumerable<string> sortedAssemblies)
        {
            var certificateGroups = new Dictionary<string, List<string>>();
            var keyPaths = new List<string>();

            foreach (var path in sortedAssemblies)
            {
                string assemblyName = Path.GetFileNameWithoutExtension(path);

                if (SystemAssemblies.Contains(assemblyName))
                {
                    Console.WriteLine($"-- WARNING: Skipping system assembly '{assemblyName}' as it cannot be registered.");
                    continue;
                }

                try
                {
                    X509Certificate2 cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                    string thumbprint = cert.Thumbprint;

                    if (!certificateGroups.ContainsKey(thumbprint))
                    {
                        certificateGroups[thumbprint] = new List<string>();
                    }

                    certificateGroups[thumbprint].Add(path);
                }
                catch (CryptographicException)
                {
                    Console.WriteLine($"-- No certificate found for '{path}', checking for asymmetric key.");
                    keyPaths.Add(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"-- Error reading file '{path}': {ex.Message}");
                }
            }

            foreach (var keyPath in keyPaths)
            {
                string assemblyName = Path.GetFileNameWithoutExtension(keyPath);
                string keyName = $"Key_{assemblyName}";

                Console.WriteLine("USE master;");
                Console.WriteLine("GO");
                Console.WriteLine($"-- SQL Script for asymmetric key: {keyName} --");
                Console.WriteLine($@"
    CREATE ASYMMETRIC KEY [{keyName}]
    FROM EXECUTABLE FILE = '{keyPath.Replace("\\", "\\\\")}';
    GO
    CREATE LOGIN [{keyName}Login] FROM ASYMMETRIC KEY [{keyName}];
    GO
    GRANT UNSAFE ASSEMBLY TO [{keyName}Login];
    GO
    USE [WebSQL];
    GO
    CREATE ASSEMBLY [WebSQL_{assemblyName}]
    FROM '{keyPath.Replace("\\", "\\\\")}'
    WITH PERMISSION_SET = UNSAFE;
    GO
    ");
            }

            foreach (var group in certificateGroups)
            {
                string thumbprint = group.Key;
                var paths = group.Value;

                string commonCertificateName = $"Cert_{thumbprint.Substring(0, 8)}";

                Console.WriteLine("USE master;");
                Console.WriteLine("GO");
                Console.WriteLine($"-- SQL Script for certificate: {commonCertificateName} --");
                Console.WriteLine($@"
    CREATE CERTIFICATE [{commonCertificateName}]
    FROM EXECUTABLE FILE = '{paths.First().Replace("\\", "\\\\")}';
    GO
    CREATE LOGIN [{commonCertificateName}Login] FROM CERTIFICATE [{commonCertificateName}];
    GO
    GRANT UNSAFE ASSEMBLY TO [{commonCertificateName}Login];
    GO
    ");

                foreach (var path in paths)
                {
                    string assemblyName = Path.GetFileNameWithoutExtension(path);
                    Console.WriteLine($@"
    USE [WebSQL];
    GO
    CREATE ASSEMBLY [WebSQL_{assemblyName}]
    FROM '{path.Replace("\\", "\\\\")}'
    WITH PERMISSION_SET = UNSAFE;
    GO
    ");
                }
            }
        }
    }
}
