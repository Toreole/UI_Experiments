using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    private TalentNode[] nodes;

    void FUCKOFF()
    {
        OnEnable();
        Update();
    }

    IEnumerator OnEnable()
    {
        if (graph == null)
            yield break;
        _lastGraph = graph;
        nodes = graph.Nodes;
        // order of operations is absolutely vital:
        ShuffleNodes();
        // shuffle before doing any other calculations.
        CalculateNodeDepths();
        yield return null;

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
        yield return null;

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
        var nodeGroups = nodes.GroupBy(x => x.RuntimeLevel).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
        foreach (var group in nodeGroups)
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
        yield return null;

        // reverse edges:
        int[][] childNodes = new int[nodes.Length][];
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
        yield return null;

        // hard layout.
        for (int gi = 0; gi < nodeGroups.Length; gi++)
        {
            var group = nodeGroups[gi];
            int groupSize = group.Length;
            for (int i = 0; i < groupSize; i++)
            {
                var node = group[i];
                node.RuntimeInstance.anchoredPosition = new Vector2(preferredSpacing * i, -preferredSpacing * gi); // gi == depth;
            }
        }
        Debug.Log("basic layout");
        yield return null;
        // minimize crossings in each layer after the first.
        // crossings can only happen, when the nodes have at least one different parent.
        for (int gi = 1; gi < nodeGroups.Length; gi++)
        {
            var group = nodeGroups[gi];
            int groupSize = group.Length;
            for (int i = 0; i < groupSize; i++)
            {
                var node = group[i];
                var nodeTransform = node.RuntimeInstance;
                var iParents = node.ParentIndices;
                for (int j = i + 1; j < groupSize; j++)
                {
                    var otherNode = group[j];
                    var otherNodeTransform = otherNode.RuntimeInstance;
                    var jParents = otherNode.ParentIndices;

                    for (int pi = 0; pi < iParents.Length; pi++)
                    {
                        for (int pj = 0; pj < jParents.Length; pj++)
                        {
                            if (iParents[pi] == jParents[pj])
                                continue;
                            var ipPos = nodes[iParents[pi]].RuntimeInstance.anchoredPosition;
                            var iPos = nodeTransform.anchoredPosition;
                            var jpPos = nodes[jParents[pj]].RuntimeInstance.anchoredPosition;
                            var jPos = otherNodeTransform.anchoredPosition;
                            // swap to nodes positions to remove a crossing.
                            // this only works out when it is possible.
                            // if there still are crossings in this layer after this step, the parent sub-graphs need to be re-ordered.
                            if ((ipPos.x < jpPos.x && iPos.x > jPos.x) || (ipPos.x > jpPos.x && iPos.x < jPos.x))
                            {
                                node.RuntimeInstance.anchoredPosition = jPos;
                                otherNode.RuntimeInstance.anchoredPosition = iPos;
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("minimized crossings");
        yield return null;
    }

    void Update()
    {
        if (graph != _lastGraph)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            StartCoroutine(OnEnable());
        }
        if (nodes == null)
            return;

        foreach (var node in nodes)
        {
            foreach (var pi in node.ParentIndices)
            {
                Debug.DrawLine(node.RuntimeInstance.position, nodes[pi].RuntimeInstance.position);
            }
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
