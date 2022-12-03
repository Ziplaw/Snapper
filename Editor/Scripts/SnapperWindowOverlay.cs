using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Snapper
{
    [EditorToolbarElement( id, typeof (SceneView) )]
    public class SnapperWindowButtonOverlay : EditorToolbarButton
    {
        public const string id = "TakeSnap";

        public SnapperWindowButtonOverlay( )
        {
            icon = Resources.Load<Texture2D>( "Textures/snap" );

            tooltip = "Make a Snap for this scene view";
            clicked += OnClick;
        }

        private void OnClick( )
        {
            SnapperWindow.TakeSnapshot( "New Snap" );
        }
    }

    [Overlay( typeof (SceneView), "Snapper Tools" )]
    [Icon( "Textures/snap.png" )]
    public class SnapperWindowOverlay : ToolbarOverlay
    {
        public SnapperWindowOverlay( ) : base( SnapperWindowButtonOverlay.id ) { }
    }
}
