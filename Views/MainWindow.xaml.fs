﻿namespace FVim

open neovim.def
open log
open common

open ReactiveUI
open Avalonia.Markup.Xaml
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia
open Avalonia.Data
open Avalonia.ReactiveUI

type MainWindow() as this =
    inherit ReactiveWindow<MainWindowViewModel>()

    static let XProp = AvaloniaProperty.Register<MainWindow,int>("PosX")
    static let YProp = AvaloniaProperty.Register<MainWindow,int>("PosY")

    do
        #if DEBUG
        this.Renderer.DrawFps <- true
        Avalonia.DevToolsExtensions.AttachDevTools(this)
        #endif

        DragDrop.SetAllowDrop(this, true)

        this.Watch [
            this.Closing.Subscribe (fun e -> Model.OnTerminating e)
            this.Closed.Subscribe  (fun _ -> Model.OnTerminated())
            this.Bind(XProp, Binding("X", BindingMode.TwoWay))
            this.Bind(YProp, Binding("Y", BindingMode.TwoWay))

            States.Register.Notify "DrawFPS" (fun [| Bool(v) |] -> 
                trace "mainwindow" "DrawFPS: %A" v
                this.Renderer.DrawFps <- v)

            this.AddHandler(DragDrop.DropEvent, (fun _ (e: DragEventArgs) ->
                if e.Data.Contains(DataFormats.FileNames) then
                    Model.EditFiles <| e.Data.GetFileNames()
                elif e.Data.Contains(DataFormats.Text) then
                    Model.InsertText <| e.Data.GetText()
            ))

            this.AddHandler(DragDrop.DragOverEvent, (fun _ (e:DragEventArgs) ->
                e.DragEffects <- DragDropEffects.Move ||| DragDropEffects.Link ||| DragDropEffects.Copy
            ))

        ]
        AvaloniaXamlLoader.Load this

    override this.OnDataContextChanged _ =
        let ctx = this.DataContext :?> MainWindowViewModel
        let pos = PixelPoint(int ctx.X, int ctx.Y)
        let mutable firstPoschange = true
        let mutable deltaX = 0
        let mutable deltaY = 0

        trace "mainwindow" "set position: %d, %d" pos.X pos.Y
        this.Position <- pos
        this.WindowState <- ctx.WindowState
        this.Watch [
            this.PositionChanged.Subscribe (fun p ->
                if firstPoschange then
                    firstPoschange <- false
                    deltaX <- p.Point.X - pos.X
                    deltaY <- p.Point.Y - pos.Y
                    trace "mainwindow" "first PositionChanged event: %d, %d (delta=%d, %d)" p.Point.X p.Point.Y deltaX deltaY
                else
                    this.SetValue(XProp, p.Point.X - deltaX)
                    this.SetValue(YProp, p.Point.Y - deltaY)
                )
        ]

