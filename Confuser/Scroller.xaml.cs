using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;

namespace Confuser
{
	/// <summary>
	/// Interaction logic for Scroller.xaml
	/// </summary>
    public class Scroller : ContentControl
    {
        static Scroller()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Scroller), new FrameworkPropertyMetadata(typeof(Scroller)));
        }

        public Scroller()
        {
            DependencyPropertyDescriptor.FromProperty(ActualWidthProperty, typeof(Scroller)).AddValueChanged(this, new EventHandler(ActualWidthChanged));
            tmr.Interval = TimeSpan.FromMilliseconds(50);
            tmr.Tick += delegate { Ping(); };
        }

        public static readonly DependencyProperty ScrollFactorProperty = DependencyProperty.Register("ScrollFactor", typeof(double), typeof(Scroller), new PropertyMetadata(0.0, ScrollFactorChanged));
        public double ScrollFactor
        {
            get { return (double)GetValue(ScrollFactorProperty); }
            set { SetValue(ScrollFactorProperty, value); }
        }

        public static readonly DependencyProperty PingTriggerProperty = DependencyProperty.Register("PingTrigger", typeof(bool), typeof(Scroller), new PropertyMetadata(false, ScrollFactorChanged, CoercePingTrigger));
        public bool PingTrigger
        {
            get { return (bool)GetValue(PingTriggerProperty); }
            set { SetValue(PingTriggerProperty, value); }
        }

        static object CoercePingTrigger(DependencyObject d, object baseValue)
        {
            if ((bool)baseValue == true)
                (d as Scroller).Ping();
            return false;
        }

        static void ScrollFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Scroller sr = d as Scroller;
            if (Math.Round((double)e.NewValue, 2) != (double)e.NewValue)
                sr.ScrollFactor = Math.Round((double)e.NewValue, 2);
            if ((double)e.OldValue == 0.0 && (double)e.NewValue != 0.0 && !sr.tmr.IsEnabled)
            {
                sr.tmr.Start();
            }
        }

        static void ActualWidthChanged(object sender, EventArgs e)
        {
            FrameworkElement cnt = Helper.FindChild<FrameworkElement>(sender as Scroller, "content");
            Grid grid = cnt.Parent as Grid;
            LinearGradientBrush grad = (grid.OpacityMask as LinearGradientBrush).Clone();
            grad.StartPoint = new Point((sender as Scroller).ActualWidth - 0, grad.StartPoint.Y);
            grid.OpacityMask = grad;

            grid = Helper.FindChild<Grid>(sender as Scroller, "scroll");
            grad = (grid.Background as LinearGradientBrush).Clone();
            grad.StartPoint = new Point((sender as Scroller).ActualWidth - 0, grad.StartPoint.Y);
            grid.Background = grad;
        }

        DispatcherTimer tmr = new DispatcherTimer();
        void Ping()
        {
            FrameworkElement left = Helper.FindChild<FrameworkElement>(this, "left");
            FrameworkElement right = Helper.FindChild<FrameworkElement>(this, "right");
            ContentPresenter cnt = Helper.FindChild<ContentPresenter>(this, "content");

            if (left.IsMouseOver)
            {
                ScrollFactor += 5;
            }
            else if (right.IsMouseOver)
            {
                ScrollFactor -= 5;
            }
            else
            {
                ScrollFactor = Math.Sign(ScrollFactor) * (Math.Max(Math.Abs(ScrollFactor) * 0.80, 0));
                if (Math.Abs(ScrollFactor) < 0.1) ScrollFactor = 0;
            }

            double leftM = cnt.Margin.Left + ScrollFactor;
            if (leftM > 50 && ScrollFactor > 0)
            {
                ScrollFactor = 0;
                leftM = 50;
            }
            else if (leftM < -(cnt.Content as FrameworkElement).ActualWidth + (this.ActualWidth - 50) && ScrollFactor < 0)
            {
                ScrollFactor = 0;
                leftM = -(cnt.Content as FrameworkElement).ActualWidth + (this.ActualWidth - 50);
            }

            Storyboard sb = new Storyboard();
            ThicknessAnimation ani = new ThicknessAnimation();
            Storyboard.SetTarget(ani, cnt);
            Storyboard.SetTargetProperty(ani, new PropertyPath(FrameworkElement.MarginProperty));
            ani.To = new Thickness(leftM, cnt.Margin.Top, cnt.Margin.Right, cnt.Margin.Bottom);
            ani.Duration = new Duration(TimeSpan.FromMilliseconds(100));
            sb.Children.Add(ani);
            sb.Begin();

            if (ScrollFactor == 0) tmr.Stop();
        }

        public void ScrollToEnd()
        {
            ScrollFactor = 0;
            ContentPresenter cnt = Helper.FindChild<ContentPresenter>(this, "content");
            Storyboard sb = new Storyboard();
            ThicknessAnimation ani = new ThicknessAnimation();
            Storyboard.SetTarget(ani, cnt);
            Storyboard.SetTargetProperty(ani, new PropertyPath(FrameworkElement.MarginProperty));
            ani.To = new Thickness(-(cnt.Content as FrameworkElement).ActualWidth + (this.ActualWidth - 50), cnt.Margin.Top, cnt.Margin.Right, cnt.Margin.Bottom);
            ani.Duration = new Duration(TimeSpan.FromMilliseconds(100));
            sb.Children.Add(ani);
            sb.Begin();
        }
        public void ScrollToBeginning()
        {
            ScrollFactor = 0;
            ContentPresenter cnt = Helper.FindChild<ContentPresenter>(this, "content");
            Storyboard sb = new Storyboard();
            ThicknessAnimation ani = new ThicknessAnimation();
            Storyboard.SetTarget(ani, cnt);
            Storyboard.SetTargetProperty(ani, new PropertyPath(FrameworkElement.MarginProperty));
            ani.To = new Thickness(50, cnt.Margin.Top, cnt.Margin.Right, cnt.Margin.Bottom);
            ani.Duration = new Duration(TimeSpan.FromMilliseconds(100));
            sb.Children.Add(ani);
            sb.Begin();
        }
    }
}