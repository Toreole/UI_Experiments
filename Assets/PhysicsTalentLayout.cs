using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Math = System.Math;

public class PhysicsTalentLayout : MonoBehaviour
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
    private float damping = 0.5f;
    [SerializeField]
    private float breakForce = 200f;
    [SerializeField]
    private float frequency = 2;

    private TalentNode[] nodes;
    private TalentNode[][] groups;

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
            var instance = Instantiate(nodePrefab, transform).GetComponent<Rigidbody2D>();
            instance.position = new Vector2(0, -preferredSpacing * (node.RuntimeLevel + 1));
            node.Body = instance;
            instance.name = node.Name + $" (l:{node.RuntimeLevel}, c:{node.NodeClass}, i:[{node.NodeIndex}], p:[{string.Join(",", node.ParentIndices)}])";
        }

        
        // base positioning around all inner nodes (which have parents and children)
        var offset = -(layoutWidth / 2f);
        //groups will be ordered by depth ascending.
        groups = nodes.GroupBy(x => x.RuntimeLevel).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
        foreach (var group in groups)
        {
            // add Spring joints between nodes on each layer.
            for (int i = 0; i < group.Length; i++)
            {
                var node = group[i];
                for (int j = i+1; j < group.Length; j++)
                {
                    var joint = node.Body.gameObject.AddComponent<SpringJoint2D>();
                    joint.connectedBody = group[j].Body;
                    joint.distance = preferredSpacing * (NodesShareParent(node, group[j])? 1 : 3);
                    joint.frequency = 1;
                    joint.dampingRatio = 1;
                    joint.breakForce = float.PositiveInfinity;
                    joint.autoConfigureDistance = false;
                }
            }
        }
        // add spring joints between nodes and their parent nodes.
        foreach (var node in nodes)
        {
            foreach (var parentIndex in node.ParentIndices)
            {
                var joint = node.Body.gameObject.AddComponent<SpringJoint2D>();
                joint.connectedBody = nodes[parentIndex].Body;
                joint.distance = preferredSpacing * Math.Abs(node.RuntimeLevel - nodes[parentIndex].RuntimeLevel);
                joint.breakForce = breakForce;
                joint.dampingRatio = damping;
                joint.frequency = frequency;
                joint.autoConfigureDistance = false;
            }
        }
        //foreach(var node in nodes)
           //node.Body.AddForce(new Vector2(UnityEngine.Random.Range(-20, 20), 0), ForceMode2D.Impulse);

    }

    private bool NodesShareParent(TalentNode node, TalentNode talentNode)
    {
        var a = node.ParentIndices;
        var b = talentNode.ParentIndices;
        for (var i = 0; i < a.Length; i++) 
        {
            for (var j = 0; j < b.Length; j++)
            {
                if (a[i] == b[j])
                    return true;
            }
        }
        return false;
    }

    float lastCheck = 0;
    int lastFailGroup = -1;
    private void FixedUpdate()
    {
        var currentTime = Time.time;
        if (lastCheck < currentTime - 1f)
        {
            lastCheck = currentTime;
            bool anyFail = false;
            for (int i = 1; i < groups.Length; i++)
            {
                if (GroupHasCrossingsToParentLayer(groups[i]))
                {
                    anyFail = true;
                    if (lastFailGroup == i)
                    {
                        // swap parents somehow
                        RandomizeGroupPositions(groups[i - 1]);
                    } 
                    else
                    {
                        MinimizeCrossingsInGroup(groups[i]);
                    }
                    lastFailGroup = i;
                }
            }
            if (!anyFail)
            {
                lastFailGroup = -1;
            }
        }
    }

    private void RandomizeGroupPositions(TalentNode[] group)
    {
        foreach (var node in group)
        {
            node.Body.position = new Vector2(UnityEngine.Random.Range(0, group.Length * preferredSpacing), node.Body.position.y);
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
            (first.Body.position, second.Body.position)
                = (second.Body.position, first.Body.position);
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
                var ipPos = nodes[firstParents[pi]].Body.position;
                var iPos = first.Body.position;
                var jpPos = nodes[secondParents[pj]].Body.position;
                var jPos = second.Body.position;
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
        }
        if (nodes == null)
            return;

        foreach (var node in nodes)
        {
            foreach (var pi in node.ParentIndices)
            {
                Color color = node.RuntimeLevel - nodes[pi].RuntimeLevel > 1 ? Color.red : Color.white;
                Debug.DrawLine(node.Body.position, nodes[pi].Body.position, color);
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
