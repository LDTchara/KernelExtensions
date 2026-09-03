using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hacknet;
using Pathfinder;
using KernelExtensions;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Pathfinder.Executable;

namespace KernelExtensions.FileEditor;

public class FileEditorEXE : Pathfinder.Executable.GameExecutable
{
    public FileEditorEXE() : base()
    {
        this.ramCost = 0;//你的内存占用
        this.IdentifierName = "Editor";//你的程序在ram栏显示的名称
        //this.CanBeKilled = false;
        //输入this.查看更多可定义的项目
    }

    public override void OnInitialize()//首次执行：初始化 ImGui 窗体
    {
        base.OnInitialize();
        foreach(var exe in os.exes)
        {
            if(exe is FileEditorEXE)
            {
                exe.needsRemoval = true;
            }
        }
        // 参数校验：需要且仅需要文件名
        if (Args.Length != 2 || string.IsNullOrEmpty(Args[1])) 
        {
            os.write("Unexcepted Args");
            return;
        }

        // 打开文件标签页（附带来源电脑/完整路径上下文，供 Save 按钮 / Ctrl+S 写回）
        int tabIndex = FileEditorWindow.OpenFileInEditorWithContext(os, Args[1], clean: false);
        if (tabIndex < 0)
        {
            os.write($"File \"{Args[1]}\" not found");
            return;
        }

        FileEditorWindow.Visible = true;
        os.write($"Opened \"{Args[1]}\" in File Editor");
    }

    public override void Draw(float t)//绘制程序窗口
    {
        base.Draw(t);
        drawTarget();
        drawOutline();
    }

    private float lifetime = 0f;
    public override void Update(float t)//循环触发（每帧）
    {
        base.Update(t);
        lifetime += t;

        // 在 Update 中更新 ImGui 窗体状态（由 FileEditorPatch 的每帧驱动绘制）
        if (FileEditorWindow.Visible)
        {
            FileEditorWindow.HandleShortcuts();
        }

        // 若玩家在窗体上点了 X（Visible 变 false），本程序仍在 RAM 里运行：
        // 可在这里选择退出或保持运行。当前保持运行（玩家可再次打开）。
    }
}