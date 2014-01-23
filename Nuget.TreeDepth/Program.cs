using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace Nuget.TreeDepth
{
    class Program
    {

        static List<Node> nodes;
        static Dictionary<string, IEnumerable<PackageDependency>> dictPackages;


        static void Main(string[] args)
        {
            // 1. install the nuget.downloader package in the package manager console: install-package nuget.downloader
            // 2. download all packages to c:\nuget with: Download-Packages -destinationDirectory c:\nuget -top $null
            // 3. run it

            //get the folder
            DirectoryInfo folder = new DirectoryInfo("c:\\nuget");

            //iterate over every package - the api is in the nuget package Nuget.Core

            IEnumerable<FileInfo> files = folder
                .GetFiles("*.nupkg");

            List<ZipPackage> zipPackages = new List<ZipPackage>();
            foreach (var file in files)
            {
                try
                {
                    zipPackages.Add(new ZipPackage(file.FullName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("broken package: {0} Exception: {1}", file.Name, ex.Message);
                }
            }
            //.GetFiles("AngularJs.SignalR.Hub.1.0.0.nupkg")
            //.Take(500)

            IEnumerable<ZipPackage> distinct = zipPackages
                .GroupBy(cust => cust.Id) //distinct it
                .Select(grp => grp.First());


            dictPackages = distinct
                .ToDictionary(x => x.Id, x => x.DependencySets.SelectMany(y => y.Dependencies));

            nodes = new List<Node>();

            //remove all without dependencies - they have depth 1
            var count1 = dictPackages.Count(x => !x.Value.Any());

            var firstLevel = dictPackages.Where(x => x.Value.Any())
                //.Where(x => x.Key.Equals("Microsoft.AspNet.SignalR"))
                //.Where(x => x.Key.Equals("MyWebApplicationPackage")) // <= circular dependency
                //.Where(x => x.Key.Equals("ScaffR"))
                //.Where(x => x.Key.StartsWith("ScaffR"))
                .Select(x => x.Key)
                //.Take(5000)
                .ToList();

            Node root = new Node("-- Nuget ROOT --");

            BuildSubTree(root, firstLevel);

            int maxDepth = nodes.Max(x => x.Depth);
            string[] deepestNodes = nodes
                .Where(y => y.Depth
                    .Equals(maxDepth))
                .Select(x => x.Id)
                .Distinct()
                .ToArray();

            Console.WriteLine();
            Console.WriteLine("Deepest level: {0}", maxDepth);
            Console.WriteLine("Deepest node: {0}", string.Join(", ", deepestNodes));
            Console.WriteLine();

            foreach (var deepestNode in deepestNodes)
            {
                var node = nodes.First(x => x.Id.Equals(deepestNode) && x.Depth == maxDepth);
                Console.WriteLine("hierarchy for {0} (bottom up):", node);
                node.Parent.Print();
                Console.WriteLine();
            }

            Console.WriteLine("done.");
            Console.ReadKey();
        }



        private static Node BuildSubTree(Node parent, List<string> firstLevel)
        {
            foreach (string dep in firstLevel.ToList())
            {
                Console.WriteLine("Dive for: {0}", dep);

                //foreach package build a node
                Node currentNode = new Node(dep);
                currentNode.Depth = parent.Depth + 1;
                currentNode.Parent = parent;
                parent.Childs.Add(currentNode);

                //add the nodes to the flat list for easier depth analyzing
                nodes.Add(currentNode);

                //get the referenced package
                if (!dictPackages.ContainsKey(dep))
                {
                    Console.WriteLine("Referenced package not found: {0}", dep);
                    continue;
                }

                var dependencies = dictPackages[dep].ToList();
                if (dependencies.Any())
                {

                    if (parent.IsCircular(dep))
                    { continue; }

                    //node has childs - go deeper (recurse)
                    List<string> nextLevelDependencies = dependencies.Select(x => x.Id).ToList();
                    BuildSubTree(currentNode, nextLevelDependencies);
                }
            }
            return parent;
        }


        public class Node
        {
            private HashSet<Node> _childs = new HashSet<Node>();

            public Node(string id)
            {
                Id = id;
            }

            public bool IsCircular(string dependency)
            {
                //wow, thanks MyWebApplicationPackage for this 
                var p = this;
                while (p != null)
                {
                    if (dependency == p.Id)
                    {
                        Console.WriteLine("circular reference");
                        return true;
                    }

                    //go up
                    p = p.Parent;
                }
                return false;
            }

            public Node Parent { get; set; }

            public int Depth { get; set; }

            public string Id { get; set; }

            public HashSet<Node> Childs
            {
                get { return _childs; }
                set { _childs = value; }
            }

            public override bool Equals(object obj)
            {
                Node node = (Node)obj;
                return node.Id.Equals(Id) && node.Depth.Equals(Depth);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override string ToString()
            {
                return Id;
            }

            public void Print()
            {
                var n = this;

                while (n.Parent != null)
                {
                    Console.WriteLine(n);
                    n = n.Parent;
                }
            }
        }
    }
}
