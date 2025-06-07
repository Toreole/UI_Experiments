using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

using Random = UnityEngine.Random;

public class TalentGraphLayout : MonoBehaviour
{
    [SerializeField]
    TalentTreeGraph graph;

    private TalentTreeGraph _lastGraph;

    [SerializeField]
    private GameObject nodePrefab;

    [SerializeField]
    private float preferredSpacing = 80;
    [SerializeField]
    private float layoutWidth = 700;

    [SerializeField]
    private float driftDivider = 5;
    [SerializeField]
    private float driftCoefficient = 0.02f;
    [SerializeField]
    private int simulationIterations = 100;

    private TalentNode[] nodes;
    private TalentNode[][] groups;
    private int[][] childNodes;

    void FUCKOFF()
    {
        Start();
        Update();
    }

    void Start()
    {
        if (graph == null)
            return;
        _lastGraph = graph;
        nodes = graph.Nodes;
        // order of operations is absolutely vital:
        ShuffleNodes();
        // shuffle before doing any other calculations.
        CalculateNodeDepths();

        // instantiating the nodes.
        foreach (var node in nodes.OrderBy(x => x.RuntimeLevel))
        {
            var instance = Instantiate(nodePrefab, transform).GetComponent<RectTransform>();
            instance.anchoredPosition = new Vector2(0, -preferredSpacing * (node.RuntimeLevel + 1));
            node.RuntimeInstance = instance;
            instance.name = node.Name + $" (l:{node.RuntimeLevel}, c:{node.NodeClass}, i:[{node.NodeIndex}], p:[{string.Join(",", node.ParentIndices)}])";
            //  Debug.Log(instance.sizeDelta); 50x50
        }
        Debug.Log("instantiated");

        //now do layout.
        //goal is that nodes dont overlap one another.
        //simple first step: spread out the nodes on each level
        // desired params: 
        // - level spacing
        // - max width (for the entire graph)
        // - minimum horizontal spacing

        // how to ensure minimum entropy (less messy?)
        // min total node distance?

        // base positioning around all inner nodes (which have parents and children)
        var offset = -(layoutWidth / 2f);
        //groups will be ordered by depth ascending.
        groups = nodes.GroupBy(x => x.RuntimeLevel).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
        foreach (var group in groups)
        {
            int i = 1;
            var groupSize = group.Length;

            foreach (var node in group)
            {
                var justifiedPos = (layoutWidth / (groupSize + 1)) * i++;
                var pos = node.RuntimeInstance.anchoredPosition;
                pos.x = justifiedPos + offset;
                node.RuntimeInstance.anchoredPosition = pos;
            }
        }
        Debug.Log("groups formed");

        // reverse edges:
        childNodes = new int[nodes.Length][];
        for (int i = 0; i < nodes.Length; i++)
        {
            List<int> children = new List<int>();
            for (int j = 0; j < nodes.Length; j++)
            {
                if (nodes[j].ParentIndices.Contains(i))
                    children.Add(j);
            }
            childNodes[i] = children.ToArray();
        }
        Debug.Log("reverse edges");

        // hard layout.
        for (int gi = 0; gi < groups.Length; gi++)
        {
            var group = groups[gi];
            int groupSize = group.Length;
            for (int i = 0; i < groupSize; i++)
            {
                var node = group[i];
                node.RuntimeInstance.anchoredPosition = new Vector2(preferredSpacing * i, -preferredSpacing * gi); // gi == depth;
            }
        }
        Debug.Log("basic layout");

        for (int iter = 0; iter < simulationIterations; iter++)
        {
            DriftNodes();
        }

        return; // DONT DO ANY OF THIS
        // minimize crossings in each layer after the first.
        // crossings can only happen, when the nodes have at least one different parent.
        for (int gi = 1; gi < groups.Length; gi++)
        {
            //yield return new WaitForSeconds(0.5f);
            var group = groups[gi];
            SortGroupByAverageParentPosition(group);
           
            // there may still be crossings at this point.
            // maybe try re-ordering the parents that cause the crossing, then go up the graphs layers
            if (GroupHasCrossingsToParentLayer(group))
            {
                //yield return new WaitForSeconds(0.5f);
                Debug.Log($"still had crossings after initial sort - layer {gi}");
                SortGroupByAverageChildPosition(groups[gi-1], childNodes);
                //yield return new WaitForSeconds(0.5f);
                //re-sort current group.
                SortGroupByAverageParentPosition(group);
                //yield return new WaitForSeconds(0.5f);

                //EliminateChildCrossingsInGroup(group, childNodes);
                //var resortParents = nodeGroups[gi - 1].OrderBy(n =>
                //{
                //    if (childNodes[n.NodeIndex].Length == 0)
                //        return 0;
                //    else
                //        return childNodes[n.NodeIndex].Sum(pi => nodes[pi].RuntimeInstance.anchoredPosition.x ) / childNodes[n.NodeIndex].Length;
                //});
                //i = 0;
                //foreach (var node in resortParents)
                //{
                //    node.RuntimeInstance.anchoredPosition = new Vector2(i++ * preferredSpacing, node.RuntimeInstance.anchoredPosition.y);
                //}
            }
            // to check whether this introduces new crossings.
            // note for later: does it help for layout to know whether a layer has fewer nodes than the previous layer?
            // note for later: how do we handle when there are multiple children and one skips a layer?
        }
        Debug.Log("minimized crossings");
        //yield return null;
    }

    private void SortGroupByAverageParentPosition(TalentNode[] group)
    {
        // order nodes by the average x-axis position of their parents, if present.
        // this does not guarantee a correct layout on its own, but can simplify the process a great deal.
        var sorted = group.OrderBy(n =>
        {
            if (n.ParentIndices.Length == 0)
                return 0;
            var nodeLevel = n.RuntimeLevel;
            return n.ParentIndices.Sum(pi =>
            {
                var levelDifference = Math.Abs(nodes[pi].RuntimeLevel - nodeLevel);
                return (levelDifference == 1) ? nodes[pi].RuntimeInstance.anchoredPosition.x : 0;
            }) / n.ParentIndices.Length;
        });
        int i = 0;
        foreach (var node in sorted)
        {
            node.RuntimeInstance.anchoredPosition = new Vector2(i++ * preferredSpacing, node.RuntimeInstance.anchoredPosition.y);
        }
    }

    private void SortGroupByAverageChildPosition(TalentNode[] group, int[][] childNodes)
    {
        // order nodes by the average x-axis position of their parents, if present.
        // this does not guarantee a correct layout on its own, but can simplify the process a great deal.
        var sorted = group.OrderBy(n =>
        {
            var children = childNodes[n.NodeIndex];
            if (children.Length == 0)
                return 0;
            var nodeLevel = n.RuntimeLevel;
            return children.Sum(ci =>
            {
                var levelDifference = Math.Abs(nodes[ci].RuntimeLevel - nodeLevel);
                return (levelDifference == 1) ? nodes[ci].RuntimeInstance.anchoredPosition.x : 0;
            }) / children.Length;
        });
        int i = 0;
        foreach (var node in sorted)
        {
            node.RuntimeInstance.anchoredPosition = new Vector2(i++ * preferredSpacing, node.RuntimeInstance.anchoredPosition.y);
        }
    }

    private void EliminateChildCrossingsInGroup(TalentNode[] group, int[][] childNodes)
    {
        for (int i = 0; i < group.Length-1; i++)
        {
            var nodeA = group[i];
            for (int j = i+1; j < group.Length; j++)
            {
                var nodeB = group[j];
                EliminateChildCrossing(nodeA, childNodes[nodeA.NodeIndex], nodeB, childNodes[nodeB.NodeIndex]);
            }
        }
    }

    private void EliminateChildCrossing(TalentNode nodeA, int[] childrenOfA, TalentNode nodeB, int[] childrenOfB)
    {
        var ax = nodeA.RuntimeInstance.anchoredPosition.x;
        var bx = nodeB.RuntimeInstance.anchoredPosition.x;
        for (int i = 0; i < childrenOfA.Length; i++)
        {
            var childAX = nodes[childrenOfA[i]].RuntimeInstance.anchoredPosition.x;
            for (int j = 0; j < childrenOfB.Length; j++)
            {
                var childBX = nodes[childrenOfA[i]].RuntimeInstance.anchoredPosition.x;
                if ((ax < bx && childAX > childBX) || (ax > bx && childAX < childBX))
                {
                    (nodeA.RuntimeInstance.anchoredPosition, nodeB.RuntimeInstance.anchoredPosition) 
                        = (nodeB.RuntimeInstance.anchoredPosition, nodeA.RuntimeInstance.anchoredPosition);
                    return;
                }
            }
        }
    }

    private void MinimizeCrossingsInGroup(TalentNode[] group)
    {
        int groupSize = group.Length;
        for (int i = 0; i < groupSize; i++)
        {
            var node = group[i];
            var iParents = node.ParentIndices;
            for (int j = i + 1; j < groupSize; j++)
            {
                var otherNode = group[j];
                var jParents = otherNode.ParentIndices;
                EliminateCrossing(node, iParents, otherNode, jParents);
            }
        }
    }

    private void EliminateCrossing(TalentNode first, int[] iParents, TalentNode second, int[] jParents)
    {
        if (NodesHaveCrossing(first, iParents, second, jParents))
        {
            (first.RuntimeInstance.anchoredPosition, second.RuntimeInstance.anchoredPosition)
                = (second.RuntimeInstance.anchoredPosition, first.RuntimeInstance.anchoredPosition);
        }
    }

    private bool GroupHasCrossingsToParentLayer(TalentNode[] group)
    {
        int groupSize = group.Length;
        for (int i = 0; i < groupSize; i++)
        {
            var node = group[i];
            var iParents = node.ParentIndices;
            for (int j = i + 1; j < groupSize; j++)
            {
                var otherNode = group[j];
                var jParents = otherNode.ParentIndices;
                if (NodesHaveCrossing(node, iParents, otherNode, jParents))
                    return true;
            }
        }
        return false;
    }

    private bool NodesHaveCrossing(TalentNode first, int[] firstParents, TalentNode second, int[] secondParents)
    {
        for (int pi = 0; pi < firstParents.Length; pi++)
        {
            for (int pj = 0; pj < secondParents.Length; pj++)
            {
                if (firstParents[pi] == secondParents[pj])
                    continue;
                var ipPos = nodes[firstParents[pi]].RuntimeInstance.anchoredPosition;
                var iPos = first.RuntimeInstance.anchoredPosition;
                var jpPos = nodes[secondParents[pj]].RuntimeInstance.anchoredPosition;
                var jPos = second.RuntimeInstance.anchoredPosition;
                // swap to nodes positions to remove a crossing.
                // this only works out when it is possible.
                // if there still are crossings in this layer after this step, the parent sub-graphs need to be re-ordered.
                if ((ipPos.x < jpPos.x && iPos.x > jPos.x) || (ipPos.x > jpPos.x && iPos.x < jPos.x))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void Update()
    {
        if (graph != _lastGraph)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            Start();
            //StartCoroutine(Start());
        }
        if (nodes == null)
            return;
        //DriftNodes();
        foreach (var node in nodes)
        {
            foreach (var pi in node.ParentIndices)
            {
                Color color = node.RuntimeLevel - nodes[pi].RuntimeLevel > 1 ? Color.red : Color.white; 
                Debug.DrawLine(node.RuntimeInstance.position, nodes[pi].RuntimeInstance.position, color);
            }
        }
    }

    void DriftNodes()
    {
        foreach(var node in nodes)
        {
            float x = node.RuntimeInstance.anchoredPosition.x;
            foreach (int parentIndex in node.ParentIndices)
                x += nodes[parentIndex].RuntimeInstance.anchoredPosition.x;
            foreach (int childIndex in childNodes[node.NodeIndex])
                x += nodes[childIndex].RuntimeInstance.anchoredPosition.x;
            x /= 1 + node.ParentIndices.Length + childNodes[node.NodeIndex].Length;
            node.Drift = (x - node.RuntimeInstance.anchoredPosition.x) * driftCoefficient;
        }

        foreach (var group in groups)
        {
            var sortedGroup = group.OrderBy(n => n.RuntimeInstance.anchoredPosition.x).ToArray();
            // spacing within layer
            for (int i = 0; i < sortedGroup.Length-1; i++) {
                var node = sortedGroup[i];
                var nextNode = sortedGroup[i+1];
                var dist = nextNode.RuntimeInstance.anchoredPosition.x - node.RuntimeInstance.position.x;
                dist /= driftDivider;
                dist = 1 / (dist * dist);
                node.Drift -= dist;
                nextNode.Drift += dist;
            }
        }

        foreach (var node in nodes)
        {
            node.RuntimeInstance.anchoredPosition += new Vector2(node.Drift, 0);
        }
    }

    // ! this needs to be called first !
    private void ShuffleNodes()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            int a = Random.Range(0, nodes.Length);
            int b = Random.Range(0, nodes.Length);
            (nodes[b], nodes[a]) = (nodes[a], nodes[b]);

            foreach (var node in nodes)
            {
                var indices = node.ParentIndices;
                for (int pi = 0; pi < indices.Length; pi++)
                {
                    if (indices[pi] == a)
                        indices[pi] = b;
                    else if (indices[pi] == b)
                        indices[pi] = a;
                }
                node.ParentIndices = indices;
            }
        }
    }

    // this isnt working like i intended
    private void CalculateNodeDepths()
    {
        foreach (var node in nodes)
            node.RuntimeLevel = 0;
        // highly inefficient:
        bool changesMade;
        do
        {
            changesMade = false;

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                foreach (var pi in node.ParentIndices)
                {
                    var parentNode = nodes[pi];
                    parentNode.NodeClass = nodes[pi].HasParent ? NodeClass.Inner : NodeClass.Root;
                    if (parentNode.RuntimeLevel >= node.RuntimeLevel)
                    {
                        // a node should always have a higher level than its parents
                        node.RuntimeLevel = nodes[pi].RuntimeLevel + 1;
                        changesMade = true;
                        // sanity check
                        if (parentNode.RuntimeLevel >= nodes.Length - 1)
                        {
                            Debug.LogError("why");
                            return;
                        }
                    }
                }
            }
        } while (changesMade);
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node.NodeClass == NodeClass.Unknown)
                node.NodeClass = NodeClass.Leaf;
            node.NodeIndex = i;
        }
    }
}
