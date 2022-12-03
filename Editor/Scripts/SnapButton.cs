using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Snapper
{
	public sealed class SnapButton : BindableElement
	{
		private const string k_editButtonActive = "edit-button-active";
		private const string k_settingsButtonInactive = "settings-button-inactive";
		private const string k_labelContainerNormal = "label-container-normal";
		private const string k_labelContainerEditMode = "label-container-editmode";

		private readonly Button _editButton;
		private readonly Button _settingsAcceptButton;
		private readonly Button _settingsDeleteButton;
		private readonly Button _settingsResnapButton;

		private readonly SnapData _snapData;

		private readonly TextField _editModeTextField;
		private readonly VisualElement _labelContainer;
		private readonly VisualElement _root;
		private readonly VisualElement _settingsElement;

		private bool _editMenuActive;
		private Clickable _clickable;
		private SnapperWindow _snapperWindow;
		private VisualElement _parentScrollViewContainer;
		public SnapButton( ) : this( null ) { }

		public SnapButton( SnapData snapData )
		{
			_snapData = snapData;
			AssetDatabase.LoadAssetAtPath<VisualTreeAsset>( $"{SnapperWindow.RelativePackagePath}Editor/UIDocuments/SnapButton.uxml" ).CloneTree( this );

			if ( snapData )
			{
				var label = this.Q<Label>( "snap-label" );
				label.Bind( new SerializedObject( snapData ) );

				_root = this.Q<VisualElement>( "root" );
				_settingsElement = this.Q<VisualElement>( "settings" );
				_editButton = this.Q<Button>( "edit-button" );
				_settingsAcceptButton = this.Q<Button>( "settings-accept" );
				_settingsResnapButton = this.Q<Button>( "settings-resnap" );
				_settingsDeleteButton = this.Q<Button>( "settings-delete" );
				_editModeTextField = this.Q<TextField>( "snap-textfield" );
				_labelContainer = this.Q<VisualElement>( "label-container" );

				_labelContainer.RemoveFromClassList( k_labelContainerEditMode );
				_labelContainer.AddToClassList( k_labelContainerNormal );

				_settingsElement.AddToClassList( k_settingsButtonInactive );
				_root.style.backgroundImage = snapData.Snapshot;
				_editModeTextField.RegisterCallback<KeyDownEvent>( OnEditModeTextFieldSubmit );

				_settingsAcceptButton.clicked += OnSettingsSubmit;
				_settingsResnapButton.clicked += OnSettingsResnap;
				_settingsDeleteButton.clicked += OnSettingsDelete;
				_editButton.clicked += EnterEditMode;
				clickable = new Clickable( OnButtonClick );
			}
		}

		public Clickable clickable
		{
			get => _clickable;
			set
			{
				if ( ( _clickable != null ) && ( _clickable.target == this ) ) this.RemoveManipulator( _clickable );
				_clickable = value;

				if ( _clickable == null ) return;

				this.AddManipulator( _clickable );
			}
		}

		private void OnEditModeTextFieldSubmit( KeyDownEvent evt )
		{
			if ( ( evt.keyCode == KeyCode.KeypadEnter ) || ( evt.keyCode == KeyCode.Return ) ) EditModeTextFieldSubmit( );
		}

		private void EditModeTextFieldSubmit( )
		{
			_snapData.Name = _editModeTextField.value;
			_snapData.Snapshot.name = _editModeTextField.value;
			var assetPath = AssetDatabase.GetAssetPath( _snapData );
			AssetDatabase.RenameAsset( assetPath, _editModeTextField.value );
			AssetDatabase.SaveAssets( );
		}

		public void Init( SnapperWindow window )
		{
			_parentScrollViewContainer = _root.parent.parent.parent;

			if ( window )
			{
				_snapperWindow = window;
				clicked += ( ) => window.OnSnapButtonClick( this );
				this.Query<Button>( ).ForEach( b => b.clicked += ( ) => window.OnSnapButtonClick( this ) );
				window.snapButtonElementClicked += OnSnapButtonClick;
			}
		}

		private void OnSnapButtonClick( SnapButton obj )
		{
			if ( _editMenuActive && ( obj != this ) ) ExitEditMode( );
		}

		private void EnterEditMode( )
		{
			_editMenuActive = true;
			_editButton.ToggleInClassList( k_editButtonActive );
			_settingsElement.ToggleInClassList( k_settingsButtonInactive );
			_labelContainer.ToggleInClassList( k_labelContainerNormal );
			_labelContainer.ToggleInClassList( k_labelContainerEditMode );
			_parentScrollViewContainer.RegisterCallback<MouseDownEvent>( OnMouseDownInEditMode );

			_editModeTextField.value = _snapData.Name;
			_editModeTextField.Focus( );
		}

		private void ExitEditMode( )
		{
			_editMenuActive = false;
			_editButton.ToggleInClassList( k_editButtonActive );
			_settingsElement.ToggleInClassList( k_settingsButtonInactive );
			_labelContainer.ToggleInClassList( k_labelContainerEditMode );
			_labelContainer.ToggleInClassList( k_labelContainerNormal );
			_parentScrollViewContainer.UnregisterCallback<MouseDownEvent>( OnMouseDownInEditMode );
		}

		private void OnMouseDownInEditMode( MouseDownEvent evt )
		{
			var rect = _root.parent.localBound;

			if ( !rect.Contains( evt.mousePosition ) ) OnSettingsCancel( );
		}

		private void OnSettingsCancel( ) => ExitEditMode( );

		private void OnSettingsSubmit( )
		{
			EditModeTextFieldSubmit( );
			ExitEditMode( );
		}

		private void OnSettingsResnap( ) => SnapperWindow.TakeSnapshot( _snapData.Name, null, SnapperWindow.CaptureType.Override );

		private void OnSettingsDelete( )
		{
			if ( !EditorUtility.DisplayDialog( "Snap Delete", "Are you sure you want to delete this Snap?", "Yes", "No" ) ) return;

			var assetPath = AssetDatabase.GetAssetPath( _snapData );
			AssetDatabase.DeleteAsset( assetPath );

			var sceneAssetGUID = AssetDatabase.AssetPathToGUID( SceneManager.GetActiveScene( ).path );
			var files = Directory.GetFiles( $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}", "*.asset" );

			if ( files.Length == 0 )
			{
				Directory.Delete( $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}" );
				File.Delete( $"{Application.dataPath}/SnapperData/Editor/{sceneAssetGUID}.meta" );

				if ( Directory.GetDirectories( $"{Application.dataPath}/SnapperData/Editor" ).Length == 0 )
				{
					Directory.Delete( $"{Application.dataPath}/SnapperData", true );
					File.Delete( $"{Application.dataPath}/SnapperData.meta" );
				}

				AssetDatabase.Refresh( );
			}
		}

		private void OnButtonClick( )
		{
			var sceneView = SceneView.lastActiveSceneView;
			sceneView.pivot = _snapData.PivotPosition;
			sceneView.rotation = Quaternion.Euler( _snapData.RotationEulerAngles );
			sceneView.size = _snapData.CameraDistance * .5f;
		}

		public event Action clicked
		{
			add
			{
				if ( _clickable == null )
					clickable = new Clickable( value );
				else
					_clickable.clicked += value;
			}
			remove
			{
				if ( _clickable == null ) return;

				_clickable.clicked -= value;
			}
		}

		public new sealed class UxmlFactory : UxmlFactory<SnapButton>
		{
			public sealed class UxmlTraits : BindableElement.UxmlTraits
			{
				public UxmlTraits( ) => focusable.defaultValue = true;
			}
		}
	}
}