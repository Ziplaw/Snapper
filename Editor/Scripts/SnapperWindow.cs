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

		private const string k_invisible = "snapper-container-invisible";
		private const string k_newSnap = "New Snap";
		private const string k_overrideMessage = "You're about to create a Snap with an existing name, do you want to override the existing Snap, or create a new one?";
		private const string k_overrideTitle = "Snap Override";

		private static VisualElement _scrollViewContainer;
		private static bool _snapMenuActive;
		internal static string RelativePackagePath;

		private Vector3 _camPos;
		private Vector3 _camRot;
		private Button _createNewSnapButton;
		private VisualElement _createNewSnapElement;
		private VisualElement _newSnapperContainer;
		private string _snapName;
		public event Action<SnapButton> snapButtonElementClicked;

		public void CreateGUI( )
		{
			RelativePackagePath = AssetDatabaseExtensions.GetDirectoryOfScript<SnapperWindow>( ).Replace( @"Editor\Scripts", string.Empty );
			var snapperWindowTreePath = $@"{RelativePackagePath}Editor/UIDocuments/SnapperWindow.uxml";
			var root = rootVisualElement;

			AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( snapperWindowTreePath )!.CloneTree( root );
			_newSnapperContainer = root.Q<VisualElement>( "new-snapper-container" );
			_scrollViewContainer = root.Q<VisualElement>( "scrollview-container" );
			UpdateScrollViewContainer( );

			_createNewSnapButton = root.Q<Button>( "snapper-button" );
			_createNewSnapButton.clicked += ToggleSnapMenu;

			_createNewSnapElement = root.Q<VisualElement>( "create-new-snap" );

			_createNewSnapElement.Q<TextField>( "snap-name" ); // no custom binding stuff
			_createNewSnapElement.Q<Vector3Field>( "camera-position" ).RegisterValueChangedCallback( OnCameraPositionChange );
			_createNewSnapElement.Q<Vector3Field>( "camera-rotation" ).RegisterValueChangedCallback( OnCameraRotationChange );
			_createNewSnapElement.Q<Button>( "snap-button" ).clicked += ( ) => { TakeSnapshot( _snapName, _newSnapperContainer ); };

			_newSnapperContainer.AddToClassList( k_invisible );

			// foreach (var visualElement in createNewSnapElement.Children())
			// {
			//     // visualElement.Bind(new SerializedObject(this));
			// }

			EditorApplication.projectChanged += UpdateScrollViewContainer;
			SceneManager.activeSceneChanged += ( _, _ ) => UpdateScrollViewContainer( );
		}

		[MenuItem( "Tools/Snapper" )]
		public static void ShowWindow( )
		{
			var wnd = GetWindow<SnapperWindow>( );
			wnd.titleContent = new GUIContent( "Snapper" );
		}

		public static void TakeSnapshot( string snapName, VisualElement snapperContainer = null, CaptureType captureType = CaptureType.Ask )
		{
			SaveCameraView( SceneView.lastActiveSceneView, 1000, snapName, snapperContainer, captureType );
			UpdateScrollViewContainer( );
		}

		public void OnSnapButtonClick( SnapButton snapButton ) => snapButtonElementClicked?.Invoke( snapButton );

		private static void UpdateScrollViewContainer( )
		{
			_scrollViewContainer.Clear( );
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
					_scrollViewContainer.Add( snapButton );
					snapButton.Init( HasOpenInstances<SnapperWindow>( ) ? GetWindow<SnapperWindow>( ) : null );
				}
			}
		}

		private void ToggleSnapMenu( )
		{
			_snapMenuActive = !_snapMenuActive;

			if ( _snapMenuActive )
			{
				var sceneView = SceneView.lastActiveSceneView;

				_snapName = k_newSnap;
				_camPos = sceneView.pivot;
				_camRot = sceneView.rotation.eulerAngles;

				_newSnapperContainer.RemoveFromClassList( k_invisible );
			}
			else
				_newSnapperContainer.AddToClassList( k_invisible );

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
				if ( captureType == CaptureType.Ask ) captureType = EditorUtility.DisplayDialog( k_overrideTitle, k_overrideMessage, "Create a new one", "Override it", DialogOptOutDecisionType.ForThisSession, "OverrideOrCreateNewSnap" ) ? CaptureType.CreateNew : CaptureType.Override;

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

		private void OnCameraPositionChange( ChangeEvent<Vector3> evt )
		{
			var sceneView = SceneView.lastActiveSceneView;
			sceneView.pivot = evt.newValue;
			sceneView.Repaint( );
		}

		private void OnCameraRotationChange( ChangeEvent<Vector3> evt )
		{
			var sceneView = SceneView.lastActiveSceneView;
			sceneView.rotation = Quaternion.Euler( evt.newValue );
			sceneView.Repaint( );
		}
	}
}