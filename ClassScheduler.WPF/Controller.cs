﻿using KitX.Contract.CSharp;
using KitX.Web.Rules;
using System.Collections.Generic;
using System.Windows;

namespace ClassScheduler.WPF;

public class Controller : IController
{
    private readonly MainWindow mainwin;

    public Controller(MainWindow mainwin)
    {
        this.mainwin = mainwin;
    }

    public void Start()
    {
        mainwin.Show();
    }

    public void Pause()
    {
        mainwin.WinPause();
    }

    public void End()
    {
        mainwin.WinExit();
    }

    public List<Function> GetFunctions()
    {
        return new List<Function>()
        {
            new Function()
            {
                DisplayNames = new Dictionary<string, string>()
                {
                    { "zh-cn", "你好, 世界!" },
                    { "en-us", "Hello, World!" }
                },
                Parameters = new Dictionary<string, Dictionary<string, string>>()
                {
                    {
                        "par1",
                        new Dictionary<string, string>()
                        {
                            { "zh-cn", "参数1" },
                            { "en-us", "Parameter1" }
                        }
                    }
                },
                ParametersType = new List<string>()
                {
                    "void"
                },
                HasAppendParameters = false,
                ReturnValueType = "void"
            }
        };
    }

    public void Execute(Command cmd)
    {

    }

    public void SetRootPath(string path)
    {
        Global.RootPath = path;

        Events.Invoke(nameof(Events.OnSetRootPath));
    }

    public void SetWorkPath(string path)
    {
        MessageBox.Show($"SetWorkPath({path})");
    }

    public void SetCommandsSendBuffer(ref Queue<Command> commands)
    {

    }
}
