﻿using ClassScheduler.WPF.Utils;
using ClassScheduler.WPF.Utils.Converter;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClassScheduler.WPF.Views;

public partial class ScheduleWindow : Window
{
    private readonly Timer mainTimer;
    private readonly Timer weatherTimer;
    private readonly Timer sentenceTimer;

    private double? classProgress;
    private bool isPlayingClassOverAnimation = false;
    private bool isPlayingClassBeginAnimation = false;
    private DateTime lastUpdatedWeather = DateTime.MinValue;
    public TimeSpan timeOffset { get; set; } = new(0, 0, 0);

    public ScheduleWindow()
    {
        InitializeComponent();

        Loaded += ScheduleWindow_Loaded;

        mainTimer = new Timer()
        {
            Interval = 5 * 1000,  // 5 秒更新一次
        };

        mainTimer.Elapsed += (_, _) =>
        {
            Dispatcher.Invoke(new(() =>
            {
                UpdateDatas();
                RefreshWeatherShow();
            }));
        };
        mainTimer.Start();

        /*
        weatherTimer = new Timer() { Interval = 10 * 60 * 1000 };
        weatherTimer.Elapsed += (_, _) => RefreshWeather();
        weatherTimer.Start();
        */

        sentenceTimer = new Timer() { Interval = 10 * 60 * 1000 };
        sentenceTimer.Elapsed += (_, _) => RefreshSentence();
        sentenceTimer.Start();
    }

    private void ScheduleWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshWindowShow();

        this.MoveToBottom();
        this.SetBottom();

        Left = 0; Top = 0;

        (Resources["Storyboard_ClassBegin"] as Storyboard)!.Completed += (_, _) =>
        {
            isPlayingClassBeginAnimation = false;
        };
        (Resources["Storyboard_ClassOver"] as Storyboard)!.Completed += (_, _) =>
        {
            isPlayingClassOverAnimation = false;
            Container_ClassProgress.Visibility = Visibility.Hidden;
            Container_ClassProgress.Opacity = 1;
            Container_ClassProgress.Height = 0;

            Seperator_ClassProgress.Height = 0;
        };

        Container_ClassProgress.Height = 0;
        Seperator_ClassProgress.Height = 0;

        UpdateDatas();

        RefreshWeatherShow();

        RefreshSentence();
    }

    public void UpdateDatas()
    {
        var now = DateTime.Now;
        var today = new DateTime(now.Year, now.Month, now.Day);
        var countdownDay = DateTime.Parse(Instances.AppConfig!.AppSettings.CountdownTime);
        // var high_school_entrance_day = new DateTime(now.Year, 6, 6);

        // if (high_school_entrance_day < now)
        //     high_school_entrance_day = high_school_entrance_day.AddYears(1);

        // high_school_entrance_day -= high_school_entrance_day.TimeOfDay;

        TextBlock_Time.Text = now.ToString("HH:mm");
        TextBlock_Date.Text = now.ToString("MM 月 dd 日");
        TextBlock_WeekDay.Text = now.ToString("dddd");
        TextBlock_DaysLeft.Text = Convert.ToInt32((countdownDay - today).TotalDays).ToString();

        RefreshClasses();
    }

    public void RefreshWeather()
    {
        if (DateTime.Now - lastUpdatedWeather < new TimeSpan(0, 5, 0)) return;  // 5 分钟更新一次

        var apiKey = "b111b5b1183443ea9d78b0eefb181cfe";

        var location = Instances.AppConfig!.AppBarConfig.WeatherCityLocID;

        var apiUrl = $"https://devapi.qweather.com/v7/weather/3d?location={location}&key={apiKey}";

        Task.Run(async () =>
        {
            try
            {
                using var clientHandler = new HttpClientHandler();

                clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
                clientHandler.ServerCertificateCustomValidationCallback =
                    (sender, cert, chain, sslPolicyErrors) => true;

                using HttpClient client = new(clientHandler);

                var tryCount = 0;

                var response = await client.GetAsync(apiUrl);

                while (tryCount < 3)
                {
                    if (response.IsSuccessStatusCode) break;
                    else
                    {
                        ++tryCount;
                        response = await client.GetAsync(apiUrl);
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    lastUpdatedWeather = DateTime.Now;

                    var responseStream = await response.Content.ReadAsStreamAsync();

                    using var gzipStream = new GZipStream(responseStream, CompressionMode.Decompress);

                    using var reader = new StreamReader(gzipStream);

                    var responseBody = reader.ReadToEnd();

                    dynamic jsonDoc = JObject.Parse(responseBody);

                    var count = 0;

                    Dispatcher.Invoke(new(() => Container_WeatherData.Children.Clear()));

                    foreach (var today in jsonDoc.daily)
                    {
                        ++count;
                        if (count > 3) break;

                        var fxDate = today.fxDate;
                        var textDay = today.textDay;
                        var tempMax = today.tempMax;
                        var tempMin = today.tempMin;
                        var windDirDay = today.windDirDay;

                        Dispatcher.Invoke(new(() =>
                        {
                            var standard_textBlock = new TextBlock()
                            {
                                Foreground = new SolidColorBrush(
                                        Color.FromArgb(
                                            (byte)(0xFF - (count - 1) * 0x33),
                                            0xFF, 0xFF, 0xFF
                                        )
                                    ),
                                FontSize = Math.Floor(28 - count * 2.9),
                                HorizontalAlignment = HorizontalAlignment.Center,
                            };

                            try
                            {
                                var date = $"{fxDate}";
                                standard_textBlock.Text = $"{date[5..]} {textDay} {tempMin}-{tempMax}℃ {windDirDay}";
                            }
                            catch
                            {
                                standard_textBlock.Text = "天气数据解析失败";
                            }

                            Container_WeatherData.Children.Add(standard_textBlock);
                        }));
                    }
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(new(() =>
                {
                    Container_WeatherData.Children.Add(new TextBlock()
                    {
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 28,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Text = "天气数据获取失败"
                    });
                }));
                Console.WriteLine($"Exception: {ex.Message}");
            }
        });
    }

    public void RefreshWeatherShow()
    {
        RefreshWeather();
        var nowTime = DateTime.Now;
        try
        {
            var beginTime = DateTime.Parse(Instances.AppConfig!.AppBarConfig.WeatherRegularlyBeginTime);
            var endTime = DateTime.Parse(Instances.AppConfig!.AppBarConfig.WeatherRegularlyEndTime);

            if (Instances.AppConfig!.AppBarConfig.EnabledShowWeather)
            {
                // 允许显示天气
                if (Instances.AppConfig!.AppBarConfig.ShowWeatherRegularly)
                {
                    // 允许定时显示天气
                    if (nowTime >= beginTime && nowTime <= endTime)
                    {
                        Container_WeatherData.Visibility = Visibility.Visible;
                        Border_Weather.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Container_WeatherData.Visibility = Visibility.Collapsed;
                        Border_Weather.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // 不允许定时显示天气
                    Container_WeatherData.Visibility = Visibility.Visible;
                    Border_Weather.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // 不允许显示天气
                Container_WeatherData.Visibility = Visibility.Collapsed;
                Border_Weather.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // TODO
        }
    }

    public void RefreshSentence()
    {
        var apiUrl = "https://v1.hitokoto.cn/?c=d&c=i&encode=text";  // d: 文学  i: 诗词

        Task.Run(async () =>
        {
            try
            {
                using var clientHandler = new HttpClientHandler();

                clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
                clientHandler.ServerCertificateCustomValidationCallback =
                    (sender, cert, chain, sslPolicyErrors) => true;

                using HttpClient client = new(clientHandler);

                var tryCount = 0;

                var response = await client.GetAsync(apiUrl);

                while (tryCount < 3)
                {
                    if (response.IsSuccessStatusCode) break;
                    else
                    {
                        ++tryCount;
                        response = await client.GetAsync(apiUrl);
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();

                    Dispatcher.Invoke(new(() =>
                    {
                        TextBlock_Sentence.Text = $"{responseBody}";
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        });
    }

    private void RefreshClasses()
    {
        WrapPanel_ClassesContainer.Children.Clear();

        Instances.Classes!.Sort();

        var inClass = false;
        var passedClassesIndex = 0;
        var totalPassesClassesCount = Instances.Classes!.ClassesList.Where(
            x => x.EndTime < DateTime.Now && x.DayOfWeek == DateTime.Now.DayOfWeek.ToInt()
        ).Count() * 1.0;

        var preparationLeadTime = new TimeSpan(0, 0, Instances.AppConfig!.AppSettings.PreparationLeadTime);

        DateTime now = DateTime.Now + timeOffset;

        foreach (var classModel in Instances.Classes!.ClassesList)
        {
            if (classModel.DayOfWeek! != now.DayOfWeek.ToInt()) continue;

            var tb = new TextBlock()
            {
                Text = classModel.Name,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 32,
            };

            var nowTime = now.TimeOfDay;
            var begin = DateTime.Parse(classModel.BeginTime?.ToString("HH:mm")!).TimeOfDay;
            var end = DateTime.Parse(classModel.EndTime?.ToString("HH:mm")!).TimeOfDay;

            // 正在上的课
            if (nowTime >= begin && nowTime <= end)
            {
                inClass = true;

                classProgress = (nowTime - begin).TotalSeconds / (end - begin).TotalSeconds * 100;

                tb.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x5E, 0x5E));

                if ((nowTime - begin).TotalSeconds <= 6 && !isPlayingClassBeginAnimation)
                {
                    isPlayingClassBeginAnimation = true;
                    (Resources["Storyboard_ClassBegin"] as Storyboard)!.Begin();
                }
            }
            // 已完成的课
            else if (nowTime >= end)
            {
                ++passedClassesIndex;

                var originColor = 180;
                var colorRange = 70;
                var targetColor = (byte)(originColor + (passedClassesIndex / totalPassesClassesCount) * colorRange);

                tb.Foreground = new SolidColorBrush(
                    Color.FromRgb(targetColor, targetColor, targetColor)
                );

                if ((nowTime - end).TotalSeconds <= 6 && !isPlayingClassOverAnimation)
                {
                    isPlayingClassOverAnimation = true;
                    (Resources["Storyboard_ClassOver"] as Storyboard)?.Begin();
                }
            }
            // 打了预备铃
            else if (nowTime >= (begin - preparationLeadTime) && nowTime < begin)
            {
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0x03, 0xFC, 0xA5));

                if ((nowTime - (begin - preparationLeadTime)).TotalSeconds <= 6)
                {
                    Instances.TopmostEffectsWindow!.PlayPrepareClassAlert();
                }
            }
            // 课间, 即将打预备铃
            else if (nowTime >= (begin - new TimeSpan(0, 10, 0)) && nowTime < begin)
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0x8C, 0xC6, 0xED));

            WrapPanel_ClassesContainer.Children.Add(tb);
        }

        classProgress = inClass ? classProgress : null;

        if (isPlayingClassOverAnimation == false && classProgress is null)
        {
            Container_ClassProgress.Visibility = Visibility.Hidden;
            Container_ClassProgress.Height = 0;
            Seperator_ClassProgress.Height = 0;
        }
        else
        {
            Seperator_ClassProgress.Height = 20;

            Container_ClassProgress.Height = Double.NaN;
            Container_ClassProgress.Opacity = 1;
            Container_ClassProgress.Visibility = Visibility.Visible;
            TextBlock_ClassesProgress.Text = $"{classProgress:f2} %";

            if (isPlayingClassOverAnimation)
                TextBlock_ClassesProgress.Text = "";
        }
    }

    private void Button_OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        Instances.MainWindow?.ComplexShow();
    }

    private void Button_QuitApp_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    public void SetWebViewVisibility(bool visible)
    {
        MainWebView.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    public WebView2 GetWebView() => MainWebView;

    private void Animation_ScrollInClassOver_Completed(object sender, EventArgs e)
    {
        switch (Container_ClassOverAnimation.HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
                Container_ClassOverAnimation.HorizontalAlignment = HorizontalAlignment.Right;
                break;
            case HorizontalAlignment.Center:
                Container_ClassOverAnimation.HorizontalAlignment = HorizontalAlignment.Stretch;
                break;
            case HorizontalAlignment.Right:
                Container_ClassOverAnimation.HorizontalAlignment = HorizontalAlignment.Left;
                break;
            case HorizontalAlignment.Stretch:
                Container_ClassOverAnimation.HorizontalAlignment = HorizontalAlignment.Center;
                break;
        }
    }

    private void Animation_ScrollInClassBegin_Completed(object sender, EventArgs e)
    {
        switch (Container_ClassBeginAnimation.HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
                Container_ClassBeginAnimation.HorizontalAlignment = HorizontalAlignment.Right;
                break;
            case HorizontalAlignment.Center:
                Container_ClassBeginAnimation.HorizontalAlignment = HorizontalAlignment.Stretch;
                break;
            case HorizontalAlignment.Right:
                Container_ClassBeginAnimation.HorizontalAlignment = HorizontalAlignment.Left;
                break;
            case HorizontalAlignment.Stretch:
                Container_ClassBeginAnimation.HorizontalAlignment = HorizontalAlignment.Center;
                break;
        }
    }

    internal void PlayClassBeginAnimation()
    {
        isPlayingClassBeginAnimation = true;
        (Resources["Storyboard_ClassBegin"] as Storyboard)!.Begin();
    }

    internal void PlayClassOverAnimation()
    {
        Seperator_ClassProgress.Height = 20;

        Container_ClassProgress.Height = double.NaN;
        Container_ClassProgress.Opacity = 1;
        Container_ClassProgress.Visibility = Visibility.Visible;

        isPlayingClassOverAnimation = true;
        (Resources["Storyboard_ClassOver"] as Storyboard)?.Begin();
    }

    public void RefreshWindowShow()
    {
        if (Instances.AppConfig!.AppBarConfig.EnabledAll)
            Panel_AppBar.Visibility = Visibility.Visible;
        else
            Panel_AppBar.Visibility = Visibility.Collapsed;

        if (Instances.AppConfig!.AppBarConfig.EnabledShowTime)
        {
            TextBlock_Time.Visibility = Visibility.Visible;
            Border_Time.Visibility = Visibility.Visible;
        }
        else
        {
            TextBlock_Time.Visibility = Visibility.Collapsed;
            Border_Time.Visibility = Visibility.Collapsed;
        }

        if (Instances.AppConfig!.AppBarConfig.EnabledShowDate)
        {
            TextBlock_Date.Visibility = Visibility.Visible;
            Border_Date.Visibility = Visibility.Visible;
        }
        else
        {
            TextBlock_Date.Visibility = Visibility.Collapsed;
            Border_Date.Visibility = Visibility.Collapsed;
        }

        if (Instances.AppConfig!.AppBarConfig.EnabledShowWeekDay)
            TextBlock_WeekDay.Visibility = Visibility.Visible;
        else
            TextBlock_WeekDay.Visibility = Visibility.Collapsed;

        if (Instances.AppConfig!.AppBarConfig.EnabledShowTime ||
            Instances.AppConfig!.AppBarConfig.EnabledShowDate ||
            Instances.AppConfig!.AppBarConfig.EnabledShowWeekDay)
        {
            Container_DateTime.Visibility = Visibility.Visible;
            Border_DateTime.Visibility = Visibility.Visible;
        }
        else
        {
            Container_DateTime.Visibility = Visibility.Collapsed;
            Border_DateTime.Visibility = Visibility.Collapsed;
        }

        if (Instances.AppConfig!.AppBarConfig.EnabledShowTimeLeft)
        {
            Container_TimeLeft.Visibility = Visibility.Visible;
            Border_TimeLeft.Visibility = Visibility.Visible;
        }
        else
        {
            Container_TimeLeft.Visibility = Visibility.Collapsed;
            Border_TimeLeft.Visibility = Visibility.Collapsed;
        }

        if (Instances.AppConfig!.AppBarConfig.EnabledShowWeather)
        {
            Container_WeatherData.Visibility = Visibility.Visible;
            Border_Weather.Visibility = Visibility.Visible;
        }
        else
        {
            Container_WeatherData.Visibility = Visibility.Collapsed;
            Border_Weather.Visibility = Visibility.Collapsed;
        }

        if (Instances.AppConfig!.AppBarConfig.EnabledShowSentence)
            Container_Sentence.Visibility = Visibility.Visible;
        else
            Container_Sentence.Visibility = Visibility.Collapsed;
    }
}
