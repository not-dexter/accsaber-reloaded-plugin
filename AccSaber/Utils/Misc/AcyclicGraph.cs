using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AccSaber.Utils.Misc
{
    public class AcyclicGraph<T> where T : notnull, IEquatable<T>
    {
        private readonly Dictionary<T, Node> nodeIdToNode;

        public IReadOnlyCollection<Node> Heads { get; private set; } = null!;
        public IReadOnlyCollection<Node> Tails { get; private set; }
        public IReadOnlyDictionary<T, Node> NodeIdToNode => nodeIdToNode;

        public AcyclicGraph(List<INode<T>> nodes)
        {
            if (nodes.Count() < 2)
                throw new ArgumentException("There must be at least 2 nodes to create a graph!");

            HashSet<T> arrowIds = [];
            HashSet<T> actualIds = [];
            Dictionary<T, INode<T>> nodeIdToNodeInfo = [];

            foreach (INode<T> node in nodes)
            {
                if (!actualIds.Add(node.Id))
                    throw new ArgumentException("All ids must be distinct!");

                nodeIdToNodeInfo.Add(node.Id, node);

                arrowIds.UnionWith(node.InwardArrows);
            }

            List<T> lastNodes = [.. actualIds.Except(arrowIds).Where(id => nodeIdToNodeInfo[id].InwardArrows.Any())];

            if (!lastNodes.Any())
                throw new ArgumentException("Must have at least one tail node!");

            if (arrowIds.Except(actualIds).Any())
                throw new ArgumentException("All inward arrows must refer to existing node ids!");

            foreach (T tailId in lastNodes)
                if (!ValidateNoCyclesFromTail(tailId, nodeIdToNodeInfo))
                    throw new ArgumentException("The given nodes must not have a cycle!");

            nodeIdToNode = [with(nodeIdToNodeInfo.Count)];
            Tails = PopulateGraph(lastNodes, nodeIdToNodeInfo);
        }
        public AcyclicGraph(IEnumerable<INode<T>> nodes) : this([.. nodes]) { }
        private IReadOnlyCollection<Node> PopulateGraph(List<T> tailIds, Dictionary<T, INode<T>> nodeIdToNodeInfo)
        {
            Dictionary<T, List<T>> nodeNextNodes = [];
            Queue<T> idsToProcess = [with(tailIds)];
            HashSet<T> headIds = [];

            INode<T> current;

            List<Node> tailNodes = [with(tailIds.Count())];

            while (idsToProcess.Count > 0)
            {
                T currentId = idsToProcess.Dequeue();
                current = nodeIdToNodeInfo[currentId];

                List<T> inwardArrows = [.. current.InwardArrows.Distinct()];

                foreach (T id in inwardArrows)
                {
                    if (nodeNextNodes.TryGetValue(id, out List<T> nodes))
                        nodes.Add(currentId);
                    else
                    {
                        nodeNextNodes.Add(id, [currentId]);
                        idsToProcess.Enqueue(id);
                    }
                }

                if (inwardArrows.Count == 0)
                    headIds.Add(currentId);
            }

            List<Node> headNodes = [with(headIds.Count)];

            foreach (T headId in headIds)
                headNodes.Add(CreateNode(headId, nodeNextNodes, nodeIdToNodeInfo, 0));

            Heads = headNodes;

            return [.. tailIds.Select(id => nodeIdToNode.TryGetValue(id, out Node n) ? n : throw new Exception("Not all tail nodes were processed!"))];
        }
        private Node CreateNode(T id, Dictionary<T, List<T>> nodeNextNodes, Dictionary<T, INode<T>> nodeIdToNodeInfo, int depth)
        {
            if (nodeIdToNode.TryGetValue(id, out Node outp))
                return outp;

            if (!nodeNextNodes.TryGetValue(id, out List<T> nodes))
            {
                outp = new(nodeIdToNodeInfo[id], [], depth);
                nodeIdToNode.Add(id, outp);
                return outp;
            }

            outp = new(nodeIdToNodeInfo[id], [.. nodes.Select(nodeId => CreateNode(nodeId, nodeNextNodes, nodeIdToNodeInfo, depth + 1))], depth);
            nodeIdToNode.Add(id, outp);
            return outp;
        }
        private static bool ValidateNoCyclesFromTail(T tailId, Dictionary<T, INode<T>> nodeIdToNodeInfo)
        {
            Dictionary<T, VisitState> states = [];

            bool Visit(T id)
            {
                if (states.TryGetValue(id, out VisitState state))
                    return state == VisitState.Visited;

                states[id] = VisitState.Visiting;

                foreach (T inwardId in nodeIdToNodeInfo[id].InwardArrows)
                {
                    if (!Visit(inwardId))
                        return false;
                }

                states[id] = VisitState.Visited;
                return true;
            }

            return Visit(tailId);
        }

        public override string ToString()
        {
            StringBuilder outp = new();

            List<Node> heads = [.. Heads];

            for (int i = 0; i < heads.Count; ++i)
            {
                Stack<(Node node, int depth)> nodes = [with(heads[i].NextNodes.Select(node => (node, 1)))];

                outp.AppendLine($"Head {i + 1}: {heads[i].Current.Id}, depth = {heads[i].DistanceToHead}");

                while (nodes.Count > 0)
                {
                    var node = nodes.Pop();
                    outp.AppendLine($"{new string('\t', node.depth)}Node: {node.node.Current.Id}, depth = {node.node.DistanceToHead}");

                    foreach (Node n in node.node.NextNodes)
                        nodes.Push((n, node.depth + 1));
                }
            }

            return outp.ToString();
        }

        private enum VisitState
        {
            Visiting,
            Visited
        }

        public record Node(INode<T> Current, IReadOnlyCollection<Node> NextNodes, int DistanceToHead);
    }
    public interface INode<T> where T : notnull, IEquatable<T>
    {
        public T Id { get; }
        public IEnumerable<T> InwardArrows { get; }
    }
}
