﻿using Lastfm.Services;
using System.Windows;
using System.Windows.Controls;
using static Player.Views.ViewerOperator;

namespace Player.Views
{
	public partial class AlbumsView : ContentControl
	{
		private int CallTime = -1; //For lazy loading

		public AlbumsView() => InitializeComponent();

		private void Grid_Loaded(object sender, RoutedEventArgs e)
		{
			if (CallTime++ == 0)
				ApplyNavigations(Controller.Library.GetAlbums(), typeof(Album), typeof(AlbumView), AlbumNavigation);
		}
	}
}