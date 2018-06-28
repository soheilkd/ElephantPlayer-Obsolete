﻿using Player.Events;
using System;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
namespace Player.Controls
{
	public partial class MediaPlayer : UserControl
	{
		public static DependencyProperty IsMagnifiedProperty =
			DependencyProperty.Register(nameof(IsMagnified), typeof(bool), typeof(MediaPlayer), new PropertyMetadata(false));
		public static DependencyProperty AreControlsVisibleProperty =
			DependencyProperty.Register(nameof(AreControlsVisible), typeof(bool), typeof(MediaPlayer), new PropertyMetadata(true));
		public static DependencyProperty IsFullScreenProperty =
			DependencyProperty.Register(nameof(IsFullScreen), typeof(bool), typeof(MediaPlayer), new PropertyMetadata(false));

		public event EventHandler<InfoExchangeArgs> SomethingHappened;
		public TimeSpan Position
		{
			get => element.Position;
			set
			{
				element.Position = value;
				if (value.TotalSeconds <= 20)
				{
					PlayCountTimer.Stop();
					PlayCountTimer.Start();
				}
			}
		}
		public double Volume
		{
			get => element.Volume; set
			{
				element.Volume = value;
				switch (element.Volume)
				{
					case double n when (n <= 0.1): VolumeIcon.Kind = PackIconKind.VolumeOff;  break;
					case double n when (n <= 0.4): VolumeIcon.Kind = PackIconKind.VolumeLow; break;
					case double n when (n <= 0.7): VolumeIcon.Kind = PackIconKind.VolumeMedium; break;
					default: VolumeIcon.Kind = PackIconKind.VolumeHigh; break;
				}
				App.Settings.Volume = element.Volume;
			}
		}
		private Media _Media = new Media();
		private Timer DraggerTimer = new Timer(250) { AutoReset = false };
		private Timer MouseMoveTimer = new Timer(App.Settings.MouseOverTimeout) { AutoReset = false };
		private Timer PlayCountTimer = new Timer(120000) { AutoReset = false };
		private TimeSpan timeSpan;
		private TimeSpan TimeSpan
		{
			get => timeSpan;
			set
			{
				timeSpan = value;
				PositionSlider.Maximum = TimeSpan.TotalMilliseconds;
				PositionSlider.SmallChange = 1 * PositionSlider.Maximum / 100;
				PositionSlider.LargeChange = 5 * PositionSlider.Maximum / 100;
				TimeLabel_Full.Content = TimeSpan.ToNewString();
				Invoke(InfoType.LengthFound, TimeSpan);
			}
		}
		private bool WasMaximized, isTopMost, IsUXChangingPosition, WasMinimal;
		public bool IsFullyLoaded;
		public bool IsFullScreen
		{
			get => (bool)GetValue(IsFullScreenProperty);
			set
			{
				SetValue(IsFullScreenProperty, value);
				ParentWindow.ResizeMode = value ? ResizeMode.NoResize : ResizeMode.CanResize;
				FullScreenButton.Icon = value ? PackIconKind.FullscreenExit : PackIconKind.Fullscreen;
				VisionButton.Visibility = value ? Visibility.Hidden : Visibility.Visible;
				ParentWindow.WindowStyle = value ? WindowStyle.None : WindowStyle.SingleBorderWindow;
				if (value)
				{
					WasMaximized = ParentWindow.WindowState == WindowState.Maximized;
					if (WasMaximized)
						ParentWindow.WindowState = WindowState.Normal;
					ParentWindow.WindowState = WindowState.Maximized;
				}
				else
					ParentWindow.WindowState = WasMaximized ? WindowState.Maximized : WindowState.Normal;
			}
		}
		public bool IsTopMost
		{
			get => isTopMost;
			set
			{
				isTopMost = value;
				ParentWindow.Topmost = value;
				ParentWindow.WindowStyle = value ? WindowStyle.None : WindowStyle.SingleBorderWindow;
			}
		}
		public bool IsMagnified
		{
			get => (bool)GetValue(IsMagnifiedProperty);
			set
			{
				SetValue(IsMagnifiedProperty, value);
				if (value)
				{
					MagnifyAnimation.From = new Thickness(ActualWidth / 2, ActualHeight, ActualWidth / 2, 0);
					elementCanvas.Height = Double.NaN;
					MagnifyBoard.Begin();
					MouseMoveTimer.Start();
				}
				else
				{
					MinifyAnimation.To = new Thickness(ActualWidth / 2, ActualHeight, ActualWidth / 2, 0);
					MinifyBoard.Begin();
					IsTopMost = false;
				}
				Invoke(InfoType.Magnifiement, value);
			}
		}
		public bool AreControlsVisible
		{
			get => (bool)GetValue(AreControlsVisibleProperty);
			set
			{
				SetValue(AreControlsVisibleProperty, value);
				if (value)
				{
					FullOnBoard.Stop();
					Dispatcher.Invoke(() => FullOffBoard.Begin());
				}
				else
				{
					if (!IsMagnified || ControlsGrid.IsMouseOver)
						return;
					FullOffBoard.Stop();
					Dispatcher.Invoke(() => FullOnBoard.Begin());
				}
			}
		}
		public Window ParentWindow;
		public Taskbar.Thumb Thumb = new Taskbar.Thumb();
		private Storyboard MagnifyBoard, MinifyBoard, FullOnBoard, FullOffBoard;
		private ThicknessAnimation MagnifyAnimation, MinifyAnimation;

		public MediaPlayer()
		{
			InitializeComponent();
			MagnifyBoard = Resources["MagnifyBoard"] as Storyboard;
			MinifyBoard = Resources["MinifyBoard"] as Storyboard;
			FullOnBoard = Resources["FullOnBoard"] as Storyboard;
			FullOffBoard = Resources["FullOffBoard"] as Storyboard;
			MagnifyAnimation = MagnifyBoard.Children[0] as ThicknessAnimation;
			MinifyAnimation = MinifyBoard.Children[0] as ThicknessAnimation;

			Thumb.NextPressed += (obj, f) => Next();
			Thumb.PausePressed += (obj, f) => PlayPause();
			Thumb.PlayPressed += (obj, f) => PlayPause();
			Thumb.PrevPressed += (obj, f) => Previous();
			MouseMoveTimer.Elapsed += (_, __) => AreControlsVisible = false;
			PlayCountTimer.Elapsed += PlayCountTimer_Elapsed;
			FullOnBoard.Completed += (_, __) => Cursor = Cursors.None;
			SizeChanged += (_,__) => elementCanvas.Height = IsMagnified ? Double.NaN : 0;
			FullOffBoard.CurrentStateInvalidated += (_, __) => Cursor = Cursors.Arrow;
			PlayModeButton.Icon = (PackIconKind)Enum.Parse(typeof(PackIconKind), App.Settings.PlayMode.ToString());
			element.MediaEnded += (_, __) => Next();

			FullScreenButton.MouseUp += (_, __) => IsFullScreen = !IsFullScreen;
			VisionButton.MouseUp += (_, __) => IsMagnified = !IsMagnified;
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			RunUX();
			Volume = App.Settings.Volume;
			App.Settings.Changed += (_, __) => MouseMoveTimer = new Timer(App.Settings.MouseOverTimeout) { AutoReset = false };
			VolumeSlider.Value = Volume * 100;
			IsFullyLoaded = true;
		}

		private void PlayCountTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			_Media.PlayCount++;
			PlayCountTimer.Stop();
		}

		private void Element_MouseDown(object sender, MouseButtonEventArgs e)
		{
			DraggerTimer.Start();
			try
			{
				if (ParentWindow.WindowState != WindowState.Maximized)
					ParentWindow.DragMove();
				if (DraggerTimer.Enabled && !IsFullScreen)
					IsTopMost = !IsTopMost;
			}
			catch (Exception) { }
		}
		private async void Element_MouseMove(object sender, MouseEventArgs e)
		{
			var y = ControlsTranslation.Y;
			await Task.Delay(50);
			if (ControlsTranslation.Y < y)
				return;
			AreControlsVisible = true;
			MouseMoveTimer.Start();
		}
		
		private async void RunUX()
		{
			UX:
			await Task.Delay(250);
			if (element.NaturalDuration.HasTimeSpan && element.NaturalDuration.TimeSpan != TimeSpan)
				TimeSpan = element.NaturalDuration.TimeSpan;
			TimeLabel_Current.Content = Position.ToNewString();
			IsUXChangingPosition = true;
			PositionSlider.Value = Position.TotalMilliseconds;
			IsUXChangingPosition = false;
			goto UX;
		}

		private void Position_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (!IsUXChangingPosition)
				Position = new TimeSpan(0, 0, 0, 0, (int)PositionSlider.Value);
		}
		private async void Position_Holding(object sender, MouseButtonEventArgs e)
		{
			var but = PlayPauseButton.Icon;
			element.Pause();
			while (e.ButtonState == MouseButtonState.Pressed)
				await Task.Delay(50);
			if (but == PackIconKind.Pause)
				element.Play();
			else if (App.Settings.PlayOnPositionChange)
				Play();
		}
		private void PlayPauseButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			if (PlayPauseButton.Icon == PackIconKind.Pause)
				Pause();
			else
				Play();
		}
		private void NextButton_Clicked(object sender, MouseButtonEventArgs e) => Invoke(InfoType.NextRequest);
		private void PreviousButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			if (PositionSlider.Value > PositionSlider.Maximum / 100 * 10)
			{
				element.Stop();
				element.Play();
			}
			else
				Invoke(InfoType.PrevRequest);
		}
		private void MinimalButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			if (MinimalViewButton.Icon != PackIconKind.ChevronDoubleDown)
			{
				Invoke(InfoType.CollapseRequest);
				MinimalViewButton.Icon = PackIconKind.ChevronDoubleDown;
			}
			else
			{
				Invoke(InfoType.ExpandRequest);
				MinimalViewButton.Icon = PackIconKind.ChevronDoubleUp;
			}
		}

		private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			Volume = VolumeSlider.Value / 100;
		}

		private void PlayMode_Click(object sender, MouseButtonEventArgs e)
		{
			switch (PlayModeButton.Icon)
			{
				case PackIconKind.Repeat:
					PlayModeButton.Icon = PackIconKind.RepeatOnce;
					App.Settings.PlayMode = PlayMode.RepeatOne;
					break;
				case PackIconKind.RepeatOnce:
					PlayModeButton.Icon = PackIconKind.Shuffle;
					App.Settings.PlayMode = PlayMode.Shuffle;
					break;
				case PackIconKind.Shuffle:
					PlayModeButton.Icon = PackIconKind.Repeat;
					App.Settings.PlayMode = PlayMode.Repeat;
					break;
				default:
					break;
			}
		}
		public void Play(Media media)
		{
			MediaManager.Load(media);
			_Media = media;
			VisionButton.Visibility = media.IsVideo ? Visibility.Visible : Visibility.Hidden;
			FullScreenButton.Visibility = VisionButton.Visibility;
			if (IsFullScreen && !media.IsVideo)
				FullScreenButton.EmulateClick();
			IsMagnified = media.IsVideo && App.Settings.VisionOrientation;
			element.Source = media.Url;
			PlayCountTimer.Stop();
			PlayCountTimer.Start();
			TitleLabel.Content = media.ToString();
			Play();
			MinimalViewButton.Visibility = media.IsVideo ? Visibility.Hidden : Visibility.Visible;
			if (media.IsVideo)
			{
				if (WasMinimal)
					return;
				if (ParentWindow.ActualHeight <= 131)
				{
					MinimalViewButton.EmulateClick();
					WasMinimal = true;
				}
				else
					WasMinimal = false;
			}
			else if (WasMinimal && ParentWindow.ActualHeight > 131)
			{
				MinimalViewButton.EmulateClick();
				WasMinimal = false;
			}
		}

		private void Invoke(InfoType type, object obj = null) => SomethingHappened?.Invoke(this, new InfoExchangeArgs(type, obj));
		
		public void Play(bool emulateClick = false)
		{
			if (emulateClick)
			{
				PlayPauseButton.Icon = PackIconKind.Play;
				PlayPauseButton.EmulateClick();
			}
			else
			{
				element.Play();
				PlayPauseButton.Icon = PackIconKind.Pause;
				Thumb.SetPlayingState(true);
			}
		}
		public void Pause(bool emulateClick = false)
		{
			if (emulateClick)
			{
				PlayPauseButton.Icon = PackIconKind.Pause;
				PlayPauseButton.EmulateClick();
			}
			else
			{
				element.Pause();
				PlayPauseButton.Icon = PackIconKind.Play;
				Thumb.SetPlayingState(false);
			}
		}
		public void SlidePosition(bool toRight, bool small = true)
		{
			if (toRight) PositionSlider.Value += small ? PositionSlider.SmallChange : PositionSlider.LargeChange;
			else PositionSlider.Value -= small ? PositionSlider.SmallChange : PositionSlider.LargeChange;
		}
		public void Stop()
		{
			element.Stop();
			element.Source = null;
		}
		public void Next() => NextButton.EmulateClick();
		public void Previous() => PreviousButton.EmulateClick();
		public void PlayPause() => PlayPauseButton.EmulateClick();

	}
}
