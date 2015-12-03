﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using NodeEditorFramework;
using NodeEditorFramework.Resources;

namespace NodeEditorFramework
{
	public class NodeEditorWindow : EditorWindow 
	{
		// Information about current instance
		private static NodeEditorWindow _editor;
		public static NodeEditorWindow editor
		{
			get
			{
				AssureHasEditor ();
				return _editor;
			}
		}
		public static void AssureHasEditor () 
		{
			if (_editor == null)
			{
				CreateEditor ();
				_editor.Repaint ();
			}
		}

		// Opened Canvas:
		public NodeCanvas mainNodeCanvas;
		public NodeEditorState mainEditorState;
		public static NodeCanvas MainNodeCanvas { get { return editor.mainNodeCanvas; } }
		public static NodeEditorState MainEditorState { get { return editor.mainEditorState; } }
		public static string openedCanvasPath;

		// GUI Settings
		public static int sideWindowWidth = 400;
		private static Texture iconTexture;

		[MenuItem("Window/Node Editor")]
		public static void CreateEditor () 
		{
			_editor = GetWindow<NodeEditorWindow> ();
			_editor.minSize = new Vector2 (800, 600);
			_editor.NewNodeCanvas ();
			NodeEditor.Repaint += _editor.Repaint;
			NodeEditor.initiated = false;

			ResourceManager.Init(NodeEditor.editorPath + "Resources/");
			iconTexture = ResourceManager.LoadTexture(EditorGUIUtility.isProSkin? "Textures/Icon_Dark.png" : "Textures/Icon_Light.png");
			_editor.titleContent = new GUIContent("Node Editor", iconTexture);
		}

		/// <summary>
		/// Handle opening canvas when double-clicking asset
		/// </summary>
		[UnityEditor.Callbacks.OnOpenAsset(1)]
		public static bool AutoOpenCanvas (int instanceID, int line) 
		{
			if (Selection.activeObject != null && Selection.activeObject.GetType () == typeof(NodeCanvas))
			{
				string NodeCanvasPath = AssetDatabase.GetAssetPath (instanceID);
				NodeEditorWindow.CreateEditor ();
				EditorWindow.GetWindow<NodeEditorWindow> ().LoadNodeCanvas (NodeCanvasPath);
				return true;
			}
			return false;
		}

		public void OnDestroy () 
		{
			NodeEditor.Repaint -= _editor.Repaint;
		}

		#region GUI

		public void OnGUI () 
		{
			// Initiation
			NodeEditor.checkInit ();
			if (NodeEditor.InitiationError) 
			{
				GUILayout.Label ("Initiation failed! Check console for more information!");
				return;
			}
			AssureHasEditor ();
			if (mainNodeCanvas == null)
				NewNodeCanvas ();

			// Example of creating Nodes and Connections through code
//			CalcNode calcNode1 = NodeTypes.getDefaultNode ("calcNode").Create (new Rect (200, 200, 200, 100));
//			CalcNode calcNode2 = NodeTypes.getDefaultNode ("calcNode").Create (new Rect (600, 200, 200, 100));
//			Node.ApplyConnection (calcNode1.Outputs [0], calcNode2.Inputs [0]);

			// Specify the Canvas rect in the EditorState:
			mainEditorState.canvasRect = canvasWindowRect;
			// If you want to use GetRect:
//			Rect canvasRect = GUILayoutUtility.GetRect (600, 600);
//			if (Event.current.type != EventType.Layout)
//				mainEditorState.canvasRect = canvasRect;

			// Perform drawing with error-handling
			try
			{
				NodeEditor.DrawCanvas (mainNodeCanvas, mainEditorState);
			}
			catch (UnityException e)
			{ // on exceptions in drawing flush the canvas to avoid locking the ui.
				NewNodeCanvas ();
				Debug.LogError ("Unloaded Canvas due to exception in Draw!");
				Debug.LogException (e);
			}

			// Draw Side Window
			sideWindowWidth = Math.Min (600, Math.Max (200, (int)(position.width / 5)));
			NodeEditorGUI.StartNodeGUI ();
			GUILayout.BeginArea (sideWindowRect, GUI.skin.box);
			DrawSideWindow ();
			GUILayout.EndArea ();
			NodeEditorGUI.EndNodeGUI ();
		}

		public void DrawSideWindow () 
		{
			GUILayout.Label (new GUIContent ("Node Editor (" + mainNodeCanvas.name + ")", "Currently opened canvas: " + openedCanvasPath), NodeEditorGUI.nodeLabelBold);

			if (GUILayout.Button (new GUIContent ("Save Canvas", "Saves the canvas as a new Canvas Asset File in the Assets Folder"))) 
			{
				SaveNodeCanvas (EditorUtility.SaveFilePanelInProject ("Save Node Canvas", "Node Canvas", "asset", "Saving to a file is only needed once.", ResourceManager.resourcePath + "Saves/"));
			}
			if (GUILayout.Button (new GUIContent ("Load Canvas", "Loads the canvas from a Canvas Asset File in the Assets Folder"))) 
			{
				string path = EditorUtility.OpenFilePanel ("Load Node Canvas", ResourceManager.resourcePath + "Saves/", "asset");
				if (!path.Contains (Application.dataPath)) 
				{
					if (path != String.Empty)
						ShowNotification (new GUIContent ("You should select an asset inside your project folder!"));
					return;
				}
				path = path.Replace (Application.dataPath, "Assets");
				LoadNodeCanvas (path);
			}
			if (GUILayout.Button (new GUIContent ("New Canvas", "Creates a new Canvas (remember to save the previous one to a referenced Canvas Asset File at least once before! Else it'll be lost!)"))) 
			{
				NewNodeCanvas ();
			}

			if (GUILayout.Button (new GUIContent ("Recalculate All", "Starts to calculate from the beginning off. Usually does not need to be triggered manually."))) 
			{
				NodeEditor.RecalculateAll (mainNodeCanvas);
			}

			NodeEditorGUI.knobSize = EditorGUILayout.IntSlider (new GUIContent ("Handle Size", "The size of the handles of the Node Inputs/Outputs"), NodeEditorGUI.knobSize, 12, 20);
			mainEditorState.zoom = EditorGUILayout.Slider (new GUIContent ("Zoom"), mainEditorState.zoom, 0.6f, 2);
		}
		
		public Rect sideWindowRect 
		{
			get { return new Rect (position.width - sideWindowWidth, 0, sideWindowWidth, position.height); }
		}
		public Rect canvasWindowRect 
		{
			get { return new Rect (0, 0, position.width - sideWindowWidth, position.height); }
		}

		#endregion

		#region Save/Load
		
		/// <summary>
		/// Saves the mainNodeCanvas and it's associated mainEditorState as an asset at path
		/// </summary>
		public void SaveNodeCanvas (string path) 
		{
			NodeEditor.SaveNodeCanvas (mainNodeCanvas, path, mainEditorState);
			Repaint ();
		}
		
		/// <summary>
		/// Loads the mainNodeCanvas and it's associated mainEditorState from an asset at path
		/// </summary>
		public void LoadNodeCanvas (string path) 
		{
			// Load the NodeCanvas
			NodeCanvas nodeCanvas = NodeEditor.LoadNodeCanvas (path);
			if (nodeCanvas == null)
				return;
			mainNodeCanvas = nodeCanvas;
			
			// Load the associated MainEditorState
			List<NodeEditorState> editorStates = NodeEditor.LoadEditorStates (path);
			mainEditorState = editorStates.Find (x => x.name == "MainEditorState");
			if (mainEditorState == null)
				mainEditorState = CreateInstance<NodeEditorState> ();
			
			// Set some editor properties
			openedCanvasPath = path;
			
			NodeEditor.RecalculateAll (mainNodeCanvas);
			Repaint ();
		}

		/// <summary>
		/// Creates and opens a new empty node canvas
		/// </summary>
		public void NewNodeCanvas () 
		{
			// New NodeCanvas
			mainNodeCanvas = CreateInstance<NodeCanvas> ();
			mainNodeCanvas.name = "New Canvas";
			// New NodeEditorState
			mainEditorState = CreateInstance<NodeEditorState> ();
			mainEditorState.canvas = mainNodeCanvas;
			mainEditorState.name = "MainEditorState";
			// Set some properties
			openedCanvasPath = "";
		}
		
		#endregion
	}
}