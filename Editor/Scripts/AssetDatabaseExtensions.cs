using System.IO;
using UnityEditor;

namespace Snapper
{
	public class AssetDatabaseExtensions
	{
		public static string GetPath( string filter )
		{
			var guids = AssetDatabase.FindAssets( filter );

			if ( ( guids == null ) || ( guids.Length == 0 ) ) return null;

			return AssetDatabase.GUIDToAssetPath( guids[0] );
		}

		public static string GetPathToScript<T>( ) where T : class => GetPath( $"t:Script {typeof (T).Name}" );

		public static string GetDirectoryOfScript<T>( ) where T : class
		{
			var relativePath = GetPathToScript<T>( );

			return string.IsNullOrEmpty( relativePath ) ? null : Path.GetDirectoryName( relativePath );
		}
	}
}