using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Snapper
{
	public sealed class SnapperWindow : EditorWindow
	{
		public enum CaptureType
		{
			CreateNew, Override, Ask
		}

		private const string k_newSnap = "New Snap";
		private const string k_invisible = "snapper-container-invisible";
		private const string k_OverrideTitle = "Snap Override";
		private const string k_OverrideMessage = "You're about to create a Snap with an existing name, do you want to override the existing Snap, or create a new one?";

		private static VisualElement scrollViewContainer;
		private static bool snapMenuActive;

		public string snapName;
		public Vector3 camPos;
		public Vector3 camRot;
		private Button createNewSnapButton;
		private VisualElement createNewSnapElement;
		private VisualElement newSnapperContainer;

		public void CreateGUI( )
		{
			var root = rootVisualElement;
			AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( "Assets/Snapper/Editor/UIDocuments/SnapperWindow.uxml" )!.CloneTree( root );
			newSnapperContainer = root.Q<VisualElement>( "new-snapper-container" );
			scrollViewContainer = root.Q<VisualElement>( "scrollview-container" );
			UpdateScrollViewContainer( );

			createNewSnapButton = root.Q<Button>( "snapper-button" );
			createNewSnapButton.clicked += ToggleSnapMenu;

			createNewSnapElement = root.Q<VisualElement>( "create-new-snap" );

			createNewSnapElement.Q<TextField>( "snap-name" ); // no custom binding stuff
			createNewSnapElement.Q<Vector3Field>( "camera-position" ).RegisterValueChangedCallback( OnCameraPositionChange );
			createNewSnapElement.Q<Vector3Field>( "camera-rotation" ).RegisterValueChangedCallback( OnCameraRotationChange );
			createNewSnapElement.Q<Button>( "snap-button" ).clicked += ( ) => { TakeSnapshot( snapName, newSnapperContainer ); };

			newSnapperContainer.AddToClassList( k_invisible );

			// foreach (var visualElement in createNewSnapElement.Children())
			// {
			//     // visualElement.Bind(new SerializedObject(this));
			// }

			EditorApplication.projectChanged += UpdateScrollViewContainer;
			SceneManager.activeSceneChanged += ( _, _ ) => UpdateScrollViewContainer( );
		}

		public event Action<SnapButton> snapButtonElementClicked;

		[MenuItem( "Tools/Snapper" )]
		public static void ShowExample( )
		{
			var wnd = GetWindow<SnapperWindow>( );
			wnd.titleContent = new GUIContent( "Snapper" );
		}

		private static void UpdateScrollViewContainer( )
		{
			scrollViewContainer.Clear( );
			var sceneAssetGUID = AssetDatabase.AssetPathToGUID( SceneManager.GetActiveScene( ).path );

			if ( !Directory.Exists( $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}" ) ) Directory.CreateDirectory( $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}" );

			var files = Directory.GetFiles( $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}", "*.asset" );

			foreach ( var file in files )
			{
				var unityPath = file.Substring( Application.dataPath.Length - 6 ).Replace( '\\', '/' );
				var asset = AssetDatabase.LoadAssetAtPath<SnapData>( unityPath );
				var snapButton = new SnapButton( asset );

				if ( asset )
				{
					scrollViewContainer.Add( snapButton );
					snapButton.Init( HasOpenInstances<SnapperWindow>( ) ? GetWindow<SnapperWindow>( ) : null );
				}
			}
		}

		public static void TakeSnapshot( string snapName, VisualElement snapperContainer = null, CaptureType captureType = CaptureType.Ask )
		{
			SaveCameraView( SceneView.lastActiveSceneView, 1000, snapName, snapperContainer, captureType );
			UpdateScrollViewContainer( );
		}

		private void OnCameraRotationChange( ChangeEvent<Vector3> evt )
		{
			var sceneView = SceneView.lastActiveSceneView;
			sceneView.rotation = Quaternion.Euler( evt.newValue );
			sceneView.Repaint( );
		}

		private void OnCameraPositionChange( ChangeEvent<Vector3> evt )
		{
			var sceneView = SceneView.lastActiveSceneView;
			sceneView.pivot = evt.newValue;
			sceneView.Repaint( );
		}

		private void ToggleSnapMenu( )
		{
			snapMenuActive = !snapMenuActive;

			if ( snapMenuActive )
			{
				var sceneView = SceneView.lastActiveSceneView;

				snapName = k_newSnap;
				camPos = sceneView.pivot;
				camRot = sceneView.rotation.eulerAngles;

				newSnapperContainer.RemoveFromClassList( k_invisible );
			}
			else
				newSnapperContainer.AddToClassList( k_invisible );

			// SaveCameraView(cam, 1000);
		}

		private static void SaveCameraView( SceneView sceneView, int dimensions, string snapName, VisualElement snapperContainer, CaptureType captureType = CaptureType.Ask )
		{
			var screenTexture = new RenderTexture( dimensions, dimensions, 16 );
			sceneView.camera.targetTexture = screenTexture;
			RenderTexture.active = screenTexture;
			sceneView.camera.Render( );
			var renderedTexture = new Texture2D( dimensions, dimensions );
			renderedTexture.ReadPixels( new Rect( 0, 0, dimensions, dimensions ), 0, 0 );
			RenderTexture.active = null;

			var sceneAssetGUID = AssetDatabase.AssetPathToGUID( SceneManager.GetActiveScene( ).path );
			var fullDirectoryPath = $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}";
			var relativeDirectoryPath = $"Assets/SnapperData/Editor/{sceneAssetGUID}";
			var snapDataPath = $"{relativeDirectoryPath}/{snapName}.asset";

			if ( !Directory.Exists( fullDirectoryPath ) ) Directory.CreateDirectory( fullDirectoryPath );

			if ( AssetDatabase.LoadAssetAtPath<SnapData>( snapDataPath ) )
			{
				if ( captureType == CaptureType.Ask ) captureType = EditorUtility.DisplayDialog( k_OverrideTitle, k_OverrideMessage, "Create a new one", "Override it", DialogOptOutDecisionType.ForThisSession, "OverrideOrCreateNewSnap" ) ? CaptureType.CreateNew : CaptureType.Override;

				if ( captureType == CaptureType.CreateNew )
				{
					var iterator = 0;
					var originalSnapName = snapName;

					while ( AssetDatabase.LoadAssetAtPath<SnapData>( snapDataPath ) )
					{
						snapName = $"{originalSnapName} ({++iterator})";
						snapDataPath = $"{relativeDirectoryPath}/{snapName}.asset";
					}
				}
			}

			var snapData = CreateInstance<SnapData>( );
			snapData.Setup( snapName, renderedTexture, sceneView.pivot, sceneView.rotation.eulerAngles, sceneView.cameraDistance );
			renderedTexture.name = snapData.Name;

			AssetDatabase.CreateAsset( snapData, snapDataPath );
			AssetDatabase.AddObjectToAsset( renderedTexture, snapData );
			AssetDatabase.SaveAssets( );

			EditorGUIUtility.PingObject( snapData );
			snapperContainer?.AddToClassList( k_invisible );
		}

		public void OnSnapButtonClick( SnapButton snapButton ) => snapButtonElementClicked?.Invoke( snapButton );
	}
}