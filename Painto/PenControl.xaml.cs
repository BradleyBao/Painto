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
        public static CustomizePenWindow penWindow;
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
                SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PenItemList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            var item = (sender as GridView).SelectedItem as PenData;
            DisableWindowControl?.Invoke(this, EventArgs.Empty);
            globalClickedItem = item;
            // If double tapped before 
            if (penWindow != null)
            {
                return;
            }
            penWindow = new CustomizePenWindow(item.Thickness, item.PenColor, item);
            penWindow.UpdatePenLayout += PenWindow_UpdatePenLayout;
            penWindow.SwitchBackToDrawingMode += PenWindow_SwitchBackToDrawingMode;
            
            penWindow.Activate(); // Activate the window and ensure it gets focus
        }

        private void PenWindow_SwitchBackToDrawingMode(object sender, EventArgs e)
        {
            SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            penWindow.Close();
            penWindow = null;
        }

        private void PenWindow_UpdatePenLayout(object sender, EventArgs e)
        {
            PenItemList.ItemsSource = null; 
            PenItemList.ItemsSource = ItemsSource;
            SaveData?.Invoke(this, EventArgs.Empty);
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

                // 触发切换回绘画模式的事件 
                SwitchBackDrawControl?.Invoke(this, EventArgs.Empty);
            }
        }

        

        public void ShutDown()
        {
            if (penWindow != null)
            {
                lock (this)
                {
                    penWindow.Close();
                    penWindow = null;
                    
                }
                
            }
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
