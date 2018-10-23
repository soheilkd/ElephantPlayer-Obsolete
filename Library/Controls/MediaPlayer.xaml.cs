﻿using System;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Library;
using Library.Controls;
using Library.Extensions;
using Library.Taskbar;
using Player.Models;

namespace Player.Controls
{
	public partial class MediaPlayer : UserControl
	{
		public event InfoExchangeHandler<Media> MediaChanged;
		public event InfoExchangeHandler<bool> VisionChanged;
		public event EventHandler FullScreenToggled;

		public MediaQueue Queue { get; private set; } = new MediaQueue();
		public Media Current => Queue.Current;

		private Brush _BorderBack = Brushes.White;
		public Brush BorderBack
		{
			get => _BorderBack;
			set
			{
				var r = (SolidColorBrush)value;
				_BorderBack = new SolidColorBrush(new Color()
				{
					R = r.Color.R,
					G = r.Color.G,
					B = r.Color.B,
					A = 255
				});
			}
		}

		public TimeSpan Position
		{
			get => element.Position;
			set
			{
				element.Position = value;
				ResetCountTimer();
			}
		}
		public double Volume
		{
			get => element.Volume;
			set
			{
				element.Volume = value;
				switch (element.Volume)
				{
					case double n when (n <= 0.1): VolumeIcon.Type = IconType.Volume0; break;
					case double n when (n <= 0.6): VolumeIcon.Type = IconType.Volume1; break;
					default: VolumeIcon.Type = IconType.Volume2; break;
				}
			}
		}
		private bool _IsMinimal;
		public bool IsMinimal
		{
			get => _IsMinimal;
			set
			{
				_IsMinimal = value;
				VolumeBorder.Visibility = value ? Visibility.Hidden : Visibility.Visible;
			}
		}

		public ThumbManager Thumb = new ThumbManager();
		private Timer PlayCountTimer = new Timer(60000) { AutoReset = false };
		private Timer MouseMoveTimer = new Timer(5000);
		public double MouseMoveInterval
		{
			get => MouseMoveTimer.Interval;
			set => MouseMoveTimer.Interval = value;
		}
		private TimeSpan _MediaTimeSpan;
		private TimeSpan MediaTimeSpan
		{
			get => _MediaTimeSpan;
			set
			{
				_MediaTimeSpan = value;
				PositionSlider.Maximum = value.TotalMilliseconds;
				TimeLabel_Full.Content = value.ToNewString();
				PositionSlider.SmallChange = 1 * PositionSlider.Maximum / 100;
				PositionSlider.LargeChange = 5 * PositionSlider.Maximum / 100;
				Queue.Current.Length = value;
			}
		}
		private bool IsUXChangingPosition;
		public bool IsFullyLoaded;
		public bool IsFullScreen => FullScreenButton.Icon == IconType.FullScreenExit;
		public bool IsVisionOn { get; set; }
		private bool AreControlsVisible
		{
			set
			{
				Dispatcher.Invoke(() =>
				{
					if (value)
					{
						FullOnBoard.Stop();
						FullOffBoard.Begin();
					}
					else
					{
						if (!IsVisionOn || ControlsGrid.IsMouseOver)
							return;
						FullOffBoard.Stop();
						FullOnBoard.Begin();
					}
				});
			}
		}
		public bool PlayOnPositionChange { get; set; }
		public bool AutoOrinateVision { get; set; }
		private Storyboard VisionOnBoard, VisionOffBoard, FullOnBoard, FullOffBoard;

		public MediaPlayer()
		{
			InitializeComponent();
			VisionOnBoard = Resources["VisionOnBoard"] as Storyboard;
			VisionOffBoard = Resources["VisionOffBoard"] as Storyboard;
			FullOnBoard = Resources["FullOnBoard"] as Storyboard;
			FullOffBoard = Resources["FullOffBoard"] as Storyboard;

			Thumb.NextClicked += (obj, f) => Next();
			Thumb.PauseClicked += (obj, f) => PlayPause();
			Thumb.PlayClicked += (obj, f) => PlayPause();
			Thumb.PrevClicked += (obj, f) => Previous();
			MouseMoveTimer.Elapsed += (_, __) => AreControlsVisible = false;
			PlayCountTimer.Elapsed += PlayCountTimer_Elapsed;
			FullOnBoard.Completed += (_, __) => Cursor = Cursors.None;
			FullOffBoard.CurrentStateInvalidated += (_, __) => Cursor = Cursors.Arrow;
			element.MediaEnded += (_, __) => Next();
			element.MediaOpened += Element_MediaOpened;
			Resources["BorderBack"] = Brushes.Transparent;
			VisionOffBoard.Completed += delegate { ElementCanvas.Visibility = Visibility.Hidden; };
			VisionOnBoard.CurrentStateInvalidated += delegate { ElementCanvas.Visibility = Visibility.Visible; };
		}

		private void Element_MediaOpened(object sender, RoutedEventArgs e)
		{
			ResetCountTimer();
			var isVideo = element.HasVideo;
			FullScreenButton.Visibility = isVideo ? Visibility.Visible : Visibility.Hidden;
			if (IsFullScreen && !isVideo)
				FullScreenButton.EmulateClick();
			//Next seems not so readable, it just checks if AutoOrientation is on, check proper conditions where operation is needed
			if ((AutoOrinateVision && (isVideo && !IsVisionOn)) || (!isVideo && IsVisionOn))
				Element_MouseUp(this, new MouseButtonEventArgs(Mouse.PrimaryDevice, 1, MouseButton.Left));
		}

		private void ResetCountTimer()
		{
			PlayCountTimer.Stop();
			PlayCountTimer.Start();
		}
		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			RunUX();
			IsFullyLoaded = true;
			ElementCanvas.Visibility = Visibility.Hidden;
		}

		private void PlayCountTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			Queue.Current.PlayCount++;
			PlayCountTimer.Stop();
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
			if (element.NaturalDuration.HasTimeSpan && element.NaturalDuration.TimeSpan != MediaTimeSpan)
				MediaTimeSpan = element.NaturalDuration.TimeSpan;
			TimeLabel_Current.Content = Position.ToNewString();
			IsUXChangingPosition = true;
			PositionSlider.Value = Position.TotalMilliseconds;
			IsUXChangingPosition = false;
			goto UX;
		}

		private async void Position_Holding(object sender, MouseButtonEventArgs e)
		{
			IconType but = PlayPauseButton.Icon;
			element.Pause();
			while (e.ButtonState == MouseButtonState.Pressed)
				await Task.Delay(50);
			if (but == IconType.Pause)
				element.Play();
			else if (PlayOnPositionChange)
				Play();
		}
		private void Position_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (!IsUXChangingPosition)
				Position = new TimeSpan(0, 0, 0, 0, (int)PositionSlider.Value);
		}
		private void PlayPauseButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			if (PlayPauseButton.Icon == IconType.Pause)
				Pause();
			else
				Play();
		}
		private void NextButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			Play(Queue.Next());
		}
		private void Play(Media media)
		{
			Queue.ClearIsPlayings(except: media);
			element.Source = new Uri(media.Path);
			MediaChanged?.Invoke(this, new InfoExchangeArgs<Media>(media));
			Play();
		}
		public void Play(MediaQueue queue, Media media)
		{
			Queue.ClearIsPlayings();
			Queue = queue;
			Play(media);
		}
		private void PreviousButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			if (PositionSlider.Value > PositionSlider.Maximum / 100 * 10)
			{
				element.Stop();
				element.Play();
				ResetCountTimer();
			}
			else
				Play(Queue.Previous());
		}
		private void FullScreenButton_Clicked(object sender, MouseButtonEventArgs e)
		{
			FullScreenButton.Icon = FullScreenButton.Icon == IconType.FullScreen ? IconType.FullScreenExit : IconType.FullScreen;
			FullScreenToggled?.Invoke(this, null);
		}
		private void VolumeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			Volume = VolumeSlider.Value / 100;
		}

		public void Play(bool emulateClick = false)
		{
			if (emulateClick)
			{
				PlayPauseButton.Icon = IconType.Play;
				PlayPauseButton.EmulateClick();
			}
			else
			{
				element.Play();
				PlayPauseButton.Icon = IconType.Pause;
				Thumb.SetPlayingState(true);
			}
		}
		public void Pause(bool emulateClick = false)
		{
			if (emulateClick)
			{
				PlayPauseButton.Icon = IconType.Pause;
				PlayPauseButton.EmulateClick();
			}
			else
			{
				element.Pause();
				PlayPauseButton.Icon = IconType.Play;
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

		private void Element_MouseUp(object sender, MouseButtonEventArgs e)
		{
			IsVisionOn = !IsVisionOn;
			VisionOnBoard.Begin();
			VisionChanged?.Invoke(this, new InfoExchangeArgs<bool>(IsVisionOn));
			Resources["BorderBack"] = IsVisionOn ? BorderBack : Brushes.Transparent;
			AreControlsVisible = true;
			element.VerticalAlignment = IsVisionOn ? VerticalAlignment.Stretch : VerticalAlignment.Bottom;
			element.HorizontalAlignment = IsVisionOn ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
			element.Width = IsVisionOn ? double.NaN : 50d;
			element.Height = IsVisionOn ? double.NaN : 50d;
			element.Margin = IsVisionOn ? new Thickness(0) : new Thickness(6, 0, 0, 28);
			element.SetValue(Panel.ZIndexProperty, IsVisionOn ? 0 : 1);
		}

		public void Next() => NextButton.EmulateClick();
		public void Previous() => PreviousButton.EmulateClick();

		private void Button_MouseUp(object sender, MouseButtonEventArgs e)
		{
			if (IsVisionOn = !IsVisionOn)
				VisionOnBoard.Begin();
			else
				VisionOffBoard.Begin();
		}

		public void PlayPause() => PlayPauseButton.EmulateClick();

		public void ChangeVolumeBySlider(double volume)
		{
			VolumeSlider.Value = volume;
		}
	}
}
