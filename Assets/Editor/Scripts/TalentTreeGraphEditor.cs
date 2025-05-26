using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;

[CustomEditor(typeof(TalentTreeGraph))]
public class TalentTreeGraphEditor : Editor
{
	SerializedProperty nodesProperty;

	private HashSet<int> foldOutNodes;

	private Vector2 scroll = Vector2.zero;

	void OnEnable()
	{
		nodesProperty = serializedObject.FindProperty("nodes"); 
		foldOutNodes = new HashSet<int>();
	}

	public override void OnInspectorGUI()
	{
		var graph = (TalentTreeGraph)target;
		ShowNodesEditor(graph);

		// preview inside a rect like this
		//var controlRect = EditorGUILayout.GetControlRect(false, 500f);
		//EditorGUI.DrawRect(controlRect, Color.blue);
		//DrawPreview(graph.Nodes, controlRect);
		//EditorGUILayout.LabelField($"controlRect: {controlRect}");
		
		// scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(100));
		// EditorGUILayout.LabelField("scrollview label");
		// EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.LabelField("scrollview label");
		//EditorGUILayout.EndScrollView();
		
		//var vertRect = EditorGUILayout.BeginVertical();
		//EditorGUI.DrawRect(vertRect, Color.red);
		//EditorGUILayout.LabelField("text");
		//EditorGUILayout.EndVertical();

		serializedObject.ApplyModifiedProperties();
	}

	private void ShowNodesEditor(TalentTreeGraph graph)
	{
		if (nodesProperty == null)
		{
			nodesProperty = serializedObject.FindProperty("nodes");
		}
		var size = nodesProperty.arraySize;
		var nodeNames = new List<string>(size);
		for (int i = 0; i < size; i++)
		{
			nodeNames.Add(nodesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
		}

		EditorGUILayout.BeginVertical();
		EditorGUI.indentLevel++;
		for (int i = 0; i < size; i++)
		{
			bool foldout = foldOutNodes.Contains(i);
			bool newFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, $"Node <{nodeNames[i]}>:", EditorStyles.foldoutHeader);
			if (newFoldout)
			{
				var element = nodesProperty.GetArrayElementAtIndex(i);
				var nameProp = element.FindPropertyRelative("name");
				nameProp.stringValue = EditorGUILayout.TextField("node name", nameProp.stringValue);

				EditorGUILayout.LabelField("Parent Nodes:");
				var parentProp = element.FindPropertyRelative("parentIndices");
				EditorGUI.indentLevel++;
				for (int j = 0; j < parentProp.arraySize; j++)
				{
					EditorGUILayout.BeginHorizontal();
					var parentIndex = parentProp.GetArrayElementAtIndex(j).intValue;
					EditorGUILayout.LabelField("- " + nodeNames[parentIndex]);
					if (GUILayout.Button("Remove"))
					{
						parentProp.DeleteArrayElementAtIndex(j);
						break;
					}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUILayout.BeginHorizontal();
				// show node names to select as parent.
				EditorGUILayout.LabelField("Add Parent");
				int index = EditorGUILayout.Popup(-1, nodeNames.ToArray());
				if (index != -1)
				{
					parentProp.InsertArrayElementAtIndex(parentProp.arraySize);
					parentProp.GetArrayElementAtIndex(parentProp.arraySize - 1).intValue = index;
				}
				EditorGUILayout.EndHorizontal();
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();
			if (foldout && !newFoldout)
				foldOutNodes.Remove(i);
			else if (!foldout && newFoldout)
				foldOutNodes.Add(i);
		}
		EditorGUILayout.EndVertical();
		if (GUILayout.Button("Add Node"))
		{
			nodesProperty.InsertArrayElementAtIndex(nodesProperty.arraySize);
			var added = nodesProperty.GetArrayElementAtIndex(nodesProperty.arraySize - 1);
			added.FindPropertyRelative("parentIndices").ClearArray();
			added.FindPropertyRelative("name").stringValue = "";
			foldOutNodes.Add(nodesProperty.arraySize - 1);
		}
		EditorGUI.indentLevel--;
	}

	private void DrawPreview(TalentNode[] nodes, Rect area)
	{
		Dictionary<TalentNode, Vector2> nodePositions = new Dictionary<TalentNode, Vector2>();

		//find first node without parents.
		var rootNode = nodes.First(x => x.ParentIndices.Length == 0);
		nodePositions[rootNode] = new Vector2(200, 50);

		foreach (var node in nodes.Where(n => n.ParentIndices.Select(pi => nodes[pi]).Contains(rootNode)))
		{
			nodePositions[node] = nodePositions[rootNode] + new Vector2(0, 30);
		}

		foreach(var pair in nodePositions)
		{
			var rect = new Rect(area.x + pair.Value.x, area.y + pair.Value.y, 20, 20);
			var content = EditorGUIUtility.IconContent("animationdopesheetkeyframe", $"|{pair.Key.Name}");
			EditorGUI.LabelField(rect, content);
			//EditorGUI.DrawRect(rect, Color.green);
		}

	}

}
