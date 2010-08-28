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
                (d as Scroller).Ping(d, new EventArgs());
            return false;
        }

        static void ScrollFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Scroller sr = d as Scroller;
            if (Math.Round((double)e.NewValue, 2) != (double)e.NewValue)
                sr.ScrollFactor = Math.Round((double)e.NewValue, 2);
            if ((double)e.OldValue == 0.0 && (double)e.NewValue != 0.0)
            {
                sr.pingTimer = new DispatcherTimer(DispatcherPriority.Loaded);
                sr.pingTimer.Interval = TimeSpan.FromMilliseconds(50);
                sr.pingTimer.Tick += sr.Ping;
                sr.pingTimer.Start();
            }
            else if ((double)e.OldValue != 0.0 && (double)e.NewValue == 0.0)
            {
                sr.pingTimer.Stop();
            }
        }

        static void ActualWidthChanged(object sender, EventArgs e)
        {
            FrameworkElement cnt = FindChild<FrameworkElement>(sender as Scroller, "content");
            Grid grid = cnt.Parent as Grid;
            LinearGradientBrush grad = (grid.OpacityMask as LinearGradientBrush).Clone();
            grad.StartPoint = new Point((sender as Scroller).ActualWidth - 0, grad.StartPoint.Y);
            grid.OpacityMask = grad;
			
            grid = FindChild<Grid>(sender as Scroller, "scroll");
            grad = (grid.Background as LinearGradientBrush).Clone();
            grad.StartPoint = new Point((sender as Scroller).ActualWidth - 0, grad.StartPoint.Y);
            grid.Background = grad;
        }

        DispatcherTimer pingTimer;
        void Ping(object sender,EventArgs e)
        {
            FrameworkElement left = FindChild<FrameworkElement>(this, "left");
            FrameworkElement right = FindChild<FrameworkElement>(this, "right");
            FrameworkElement cnt = FindChild<FrameworkElement>(this, "content");

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
                ScrollFactor = Math.Sign(ScrollFactor) * (Math.Max(Math.Abs(ScrollFactor) * 0.8, 0));
                if (Math.Abs(ScrollFactor) < 0.1) ScrollFactor = 0;
            }

            double leftM;
            if (cnt.Margin.Left >= this.ActualWidth / 2 && ScrollFactor > 0)
            {
                ScrollFactor = 0;
                leftM = this.ActualWidth - this.ActualWidth / 2;
            }
            else if (cnt.Margin.Left <= -(cnt.ActualWidth - this.ActualWidth / 2) && ScrollFactor < 0)
            {
                ScrollFactor = 0;
                leftM = -(cnt.ActualWidth - this.ActualWidth / 2);
            }
            else
            {
                leftM = cnt.Margin.Left + ScrollFactor;
            }

            Storyboard sb = new Storyboard();
            ThicknessAnimation ani = new ThicknessAnimation();
            Storyboard.SetTarget(ani, cnt);
            Storyboard.SetTargetProperty(ani, new PropertyPath(FrameworkElement.MarginProperty));
            ani.To = new Thickness(leftM, cnt.Margin.Top, cnt.Margin.Right, cnt.Margin.Bottom);
            ani.Duration = new Duration(TimeSpan.FromMilliseconds(55));
            sb.Children.Add(ani);
            sb.Begin();
        }

        public static T FindChild<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }

        public void ScrollToEnd()
        {
            ScrollFactor = 0;
            FrameworkElement cnt = FindChild<FrameworkElement>(this, "content");
            Storyboard sb = new Storyboard();
            ThicknessAnimation ani = new ThicknessAnimation();
            Storyboard.SetTarget(ani, cnt);
            Storyboard.SetTargetProperty(ani, new PropertyPath(FrameworkElement.MarginProperty));
            ani.To = new Thickness(-(cnt.ActualWidth - this.ActualWidth / 2), cnt.Margin.Top, cnt.Margin.Right, cnt.Margin.Bottom);
            ani.Duration = new Duration(TimeSpan.FromMilliseconds(100));
            sb.Children.Add(ani);
            sb.Begin();
        }
        public void ScrollToBeginning()
        {
            ScrollFactor = 0;
            FrameworkElement cnt = FindChild<FrameworkElement>(this, "content");
            Storyboard sb = new Storyboard();
            ThicknessAnimation ani = new ThicknessAnimation();
            Storyboard.SetTarget(ani, cnt);
            Storyboard.SetTargetProperty(ani, new PropertyPath(FrameworkElement.MarginProperty));
            ani.To = new Thickness(this.ActualWidth / 2, cnt.Margin.Top, cnt.Margin.Right, cnt.Margin.Bottom);
            ani.Duration = new Duration(TimeSpan.FromMilliseconds(100));
            sb.Children.Add(ani);
            sb.Begin();
        }
    }
}