using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TheGuideToTheNewEden.WPF
{
    public partial class MainWindow
    {
        #region 异常处理
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
            System.Windows.Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            //处理非UI线程异常
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            ///Task线程内异常
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.Windows.Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        }
        /// <summary>
        /// 非UI线程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("非UI线程发生未处理的异常：");
            if (e.IsTerminating)//IsTerminating == true 将便不可避免关闭
            {
                stringBuilder.AppendLine("程序发生致命错误，将终止！");
            }
            if (e.ExceptionObject is Exception)
            {
                stringBuilder.Append(((Exception)e.ExceptionObject).Message);
            }
            else
            {
                stringBuilder.Append(e.ExceptionObject);
            }
            Core.Log.Error(stringBuilder.ToString());
        }
        /// <summary>
        /// UI线程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                e.Handled = true;
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("UI线程发生未处理的异常：");
                stringBuilder.Append(e.Exception.Message);
                Core.Log.Error(stringBuilder.ToString());
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("程序发生致命错误，将终止！", "系统错误", MessageBoxButton.OK);
            }
        }
        /// <summary>
        /// Task线程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();//避免崩溃
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Task线程发生未处理的异常：");
            stringBuilder.Append(e.Exception.Message);
            Core.Log.Error(stringBuilder.ToString());
        }
        #endregion
    }
}
