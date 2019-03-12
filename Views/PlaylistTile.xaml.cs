using Library;
using System;
using System.Windows.Controls;

namespace Player.Views
{
	public partial class PlaylistTile : UserControl
	{
		public event InfoExchangeHandler<string> Expanded;
		public event EventHandler Collapsed;
		private bool _IsStatusChangingByCode = false;

		public PlaylistTile() => InitializeComponent();

		public PlaylistTile(string playlist)
		{
			InitializeComponent();

			MainTextBlock.Text = playlist;

			MainToggle.Checked += delegate { if (!_IsStatusChangingByCode) Expanded.Invoke(this, playlist); };
			MainToggle.Unchecked += delegate { if (!_IsStatusChangingByCode) Collapsed.Invoke(this, null); };
		}

		public void ChangeStatus(bool? isChecked, bool raiseEvent = false)
		{
			_IsStatusChangingByCode = !raiseEvent;
			MainToggle.IsChecked = isChecked;
			_IsStatusChangingByCode = false;
		}
	}
}
