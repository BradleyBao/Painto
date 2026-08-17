using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Painto.Modules;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Painto
{
    public sealed partial class PenControl : UserControl
    {
        private bool suppressFlyoutClosedHandling;
        public delegate void MyEventHandler(object sender, EventArgs e);
        public event MyEventHandler DisableWindowControl;
        public event MyEventHandler SwitchBackDrawControl;
        public event MyEventHandler SaveData;
        public int Index;
        public PenData globalClickedItem;

        public ObservableCollection<PenData> ItemsSource
        {
            get { return (ObservableCollection<PenData>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(ObservableCollection<PenData>), typeof(PenControl), new PropertyMetadata(null));

        public PenControl()
        {
            this.InitializeComponent();
            Init();
        }

        private void Init()
        {
            PenItemList.SelectedIndex = 0;
            var selectedItem = (GridViewItem)PenItemList.ContainerFromIndex(0);
            selectedItem?.Focus(FocusState.Programmatic);
            //this.globalClickedItem = ItemsSource[0];
        }

        private void PenItemList_ItemClick(object sender, TappedRoutedEventArgs e)
        {
            var clickedItem = (sender as GridView).SelectedItem as PenData;
            globalClickedItem = clickedItem;
            if (clickedItem != null)
            {
                ToolBarWindow.penColor = clickedItem.PenColor;
                ToolBarWindow.penThickness = clickedItem.Thickness;
                ToolBarWindow.penOpacity = clickedItem.Opacity;
                SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PenItemList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            var gridView = sender as GridView;
            var item = gridView.SelectedItem as PenData;
            if (item == null) return;

            DisableWindowControl?.Invoke(this, EventArgs.Empty);
            globalClickedItem = item;

            var container = gridView.ContainerFromItem(item) as FrameworkElement;
            PenEditContent.LoadPen(item);
            PenEditFlyout.ShowAt(container ?? gridView);
        }

        private void PenEditContent_Applied(object sender, EventArgs e)
        {
            suppressFlyoutClosedHandling = true;
            PenItemList.ItemsSource = null;
            PenItemList.ItemsSource = ItemsSource;
            SaveData?.Invoke(this, EventArgs.Empty);
            SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            PenEditFlyout.Hide();
        }

        private void PenEditContent_Cancelled(object sender, EventArgs e)
        {
            suppressFlyoutClosedHandling = true;
            SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            PenEditFlyout.Hide();
        }

        private void PenEditFlyout_Closed(object sender, object e)
        {
            // Light-dismiss (click elsewhere) closes the flyout without going through
            // Apply/Cancel, so treat it the same as Cancel unless we already handled it.
            if (suppressFlyoutClosedHandling)
            {
                suppressFlyoutClosedHandling = false;
                return;
            }
            SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
        }

        public void SelectPen(int index)
        {
            if (ItemsSource != null && index >= 0 && index < ItemsSource.Count)
            {
                // 更新 UI 选中状态
                PenItemList.SelectedIndex = index;

                // 获取数据
                var selectedPen = ItemsSource[index];
                globalClickedItem = selectedPen;

                // 应用笔刷设置到 ToolBarWindow
                ToolBarWindow.penColor = selectedPen.PenColor;
                ToolBarWindow.penThickness = selectedPen.Thickness;
                ToolBarWindow.penOpacity = selectedPen.Opacity;

                // 触发切换回绘画模式的事件 
                SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            }
        }

        

        public void ShutDown()
        {
            suppressFlyoutClosedHandling = true;
            PenEditFlyout.Hide();
        }

        private void PenItemList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // 获取点击的项目
            var clickedItem = (e.OriginalSource as FrameworkElement)?.DataContext;
            // 找到项目的索引
            Index = PenItemList.Items.IndexOf(clickedItem);
            ItemsSource.RemoveAt(Index);
            PenItemList.ItemsSource = ItemsSource;
            SaveData?.Invoke(this, EventArgs.Empty);
        }
    }
}
