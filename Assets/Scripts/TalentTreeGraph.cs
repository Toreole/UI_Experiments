using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "new TalentTreeGraph", menuName = "Custom/Talents/Graph")]
public class TalentTreeGraph : ScriptableObject
{
    [SerializeField]
    private TalentNode[] nodes = new TalentNode[0];

    public TalentNode[] Nodes => GetCopyOfNodes();

    // deep clone of the data for safety.
    public TalentNode[] GetCopyOfNodes()
    {
        var result = new TalentNode[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = new TalentNode(nodes[i]);
            result[i] = node;
        }
        return result;
    }
}

[Serializable]
public class TalentNode
{
    [SerializeField]
    private string name = "";
    [SerializeField]
    private int[] parentIndices = new int[0];

    public TalentNode()
    {

    }

    internal TalentNode(TalentNode original)
    {
        name = original.name; //fine because string is not mutable.
        parentIndices = new int[original.parentIndices.Length];
        for (int i = 0; i < parentIndices.Length; i++)
        {
            parentIndices[i] = original.parentIndices[i];
        }
    }

    public string Name => name;
    public int[] ParentIndices
    {
        get => (int[])parentIndices.Clone();
        internal set => parentIndices = value;
    }

    public bool HasParent => parentIndices.Length > 0;
    public int RuntimeLevel { get; set; } = 0;
    public RectTransform RuntimeInstance { get; set; }
    public Vector2 Position
    {
        get => RuntimeInstance.anchoredPosition;
        set => RuntimeInstance.anchoredPosition = value;
    } 
    public float PosX
    {
        get => Position.x;
        set => Position = new Vector2(value, Position.y);
    }
    public Rigidbody2D Body { get; set; }
    public int NodeIndex { get; set; } = -1;
    public NodeClass NodeClass { get; set; } = NodeClass.Unknown;
    public float Drift { get; set; }

}

public enum NodeClass
{
    Unknown, Root, Inner, Leaf
}