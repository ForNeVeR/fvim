﻿namespace FVim

open neovim.def
open neovim.rpc
open log

open Avalonia.Controls
open Avalonia
open System
open System.Collections.Generic
open Avalonia.Threading
open ui
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.VisualTree
open Avalonia.Animation
open Avalonia.Markup.Xaml
open ReactiveUI
open System.Reactive.Linq
open System.Reactive.Disposables
open Avalonia.Data

type Cursor() as this =
    inherit Control()

    // workaround: binding directly to Canvas.Left/Top won't work.
    // so we introduce a proxy DP for x and y.
    static let PosXProp = AvaloniaProperty.Register<Cursor, float>("PosX")
    static let PosYProp = AvaloniaProperty.Register<Cursor, float>("PosY")
    static let RenderTickProp = AvaloniaProperty.Register<Cursor, int>("RenderTick")
    static let ViewModelProp = AvaloniaProperty.Register<Cursor, CursorViewModel>("ViewModel")

    let mutable cursor_timer: IDisposable = null
    let mutable bgbrush: SolidColorBrush  = SolidColorBrush(Colors.Black)
    let mutable fgbrush: SolidColorBrush  = SolidColorBrush(Colors.White)
    let mutable spbrush: SolidColorBrush  = SolidColorBrush(Colors.Red)
    //                    w     h     dpi
    let fbs = Dictionary<(float*float*float), RenderTargetBitmap> ()

    let cursorTimerRun action time =
        if cursor_timer <> null then
            cursor_timer.Dispose()
            cursor_timer <- null
        if time > 0 then
            cursor_timer <- DispatcherTimer.RunOnce(Action(action), TimeSpan.FromMilliseconds(float time), DispatcherPriority.Render)

    let showCursor (v: bool) =
        let opacity = 
            if v && this.ViewModel.enabled && this.ViewModel.ingrid
            then 1.0
            else 0.0
        this.Opacity <- opacity
        
    let rec blinkon() =
        showCursor true
        cursorTimerRun blinkoff this.ViewModel.blinkon
    and blinkoff() = 
        showCursor false
        cursorTimerRun blinkon this.ViewModel.blinkoff

    let getfb w h =
        let mutable fb = Unchecked.defaultof<RenderTargetBitmap>
        let s = this.GetVisualRoot().RenderScaling
        if not <| fbs.TryGetValue((w,h,s), &fb)
        then 
            fb <- AllocateFramebuffer w h s
            fbs.[(w,h,s)] <- fb
        fb

    let cursorConfig _ =
        //trace "cursor" "render tick %A" id
        if Object.Equals(this.ViewModel, null) 
        then ()
        else
            (* update the settings *)
            if this.ViewModel.fg <> fgbrush.Color then
                fgbrush <- SolidColorBrush(this.ViewModel.fg)
            if this.ViewModel.bg <> bgbrush.Color then
                bgbrush <- SolidColorBrush(this.ViewModel.bg)
            if this.ViewModel.sp <> spbrush.Color then
                spbrush <- SolidColorBrush(this.ViewModel.sp)
            (* reconfigure the cursor *)
            showCursor true
            cursorTimerRun blinkon this.ViewModel.blinkwait
            this.InvalidateVisual()

    let setCursorAnimation (blink_en: bool) (move_en: bool) =
        let transitions = Transitions()
        if blink_en then 
            let blink_transition = DoubleTransition()
            blink_transition.Property <- Cursor.OpacityProperty
            blink_transition.Duration <- TimeSpan.FromMilliseconds(150.0)
            blink_transition.Easing   <- Easings.LinearEasing()
            transitions.Add(blink_transition)
        if move_en then
            let x_transition = DoubleTransition()
            x_transition.Property <- PosXProp
            x_transition.Duration <- TimeSpan.FromMilliseconds(80.0)
            x_transition.Easing   <- Easings.CubicEaseOut()
            let y_transition = DoubleTransition()
            y_transition.Property <- PosYProp
            y_transition.Duration <- TimeSpan.FromMilliseconds(80.0)
            y_transition.Easing   <- Easings.CubicEaseOut()
            transitions.Add(x_transition)
            transitions.Add(y_transition)
        this.SetValue(Cursor.TransitionsProperty, transitions)

    do
        [
            Model.Notify "SetCursorAnimation" 
                (function 
                 | [| Bool(blink) |] -> setCursorAnimation blink false
                 | [| Bool(blink); Bool(move) |] -> setCursorAnimation blink move
                 | _ -> setCursorAnimation false false) 

            this.WhenActivated(fun disposables -> 
                ignore <| this.GetObservable(PosXProp).Subscribe(fun x -> this.SetValue(Canvas.LeftProperty, x, BindingPriority.Style)).DisposeWith(disposables)
                ignore <| this.GetObservable(PosYProp).Subscribe(fun y -> this.SetValue(Canvas.TopProperty, y, BindingPriority.Style)).DisposeWith(disposables)
                ignore <| this.GetObservable(RenderTickProp).Subscribe(cursorConfig)
            )
        ] |> List.iter ignore
        AvaloniaXamlLoader.Load(this)

    member this.ViewModel: CursorViewModel = this.DataContext :?> CursorViewModel

    override this.Render(ctx) =

        let cellw p = min (double(p) / 100.0 * this.Width) 1.0
        let cellh p = min (double(p) / 100.0 * this.Height) 5.0

        match this.ViewModel.shape, this.ViewModel.cellPercentage with
        | CursorShape.Block, _ ->
            let fb = getfb this.Width this.Height
            use dc = fb.CreateDrawingContext(null)
            let typeface = GetTypeface(this.ViewModel.text, this.ViewModel.italic, this.ViewModel.bold, this.ViewModel.typeface, this.ViewModel.wtypeface)
            let fg = GetForegroundBrush(this.ViewModel.fg, typeface, this.ViewModel.fontSize)
            RenderText(dc, Rect(this.Bounds.Size), fg, this.ViewModel.bg, this.ViewModel.sp, this.ViewModel.underline, this.ViewModel.undercurl, this.ViewModel.text)

            let scale = this.GetVisualRoot().RenderScaling
            ctx.DrawImage(fb, 1.0, Rect(0.0, 0.0, scale * fb.Size.Width, scale * fb.Size.Height), Rect(this.Bounds.Size))
        | CursorShape.Horizontal, p ->
            let h = (cellh p)
            let region = Rect(0.0, this.Height - h, this.Width, h)
            ctx.FillRectangle(SolidColorBrush(this.ViewModel.bg), region)
        | CursorShape.Vertical, p ->
            let region = Rect(0.0, 0.0, cellw p, this.Height)
            ctx.FillRectangle(SolidColorBrush(this.ViewModel.bg), region)

    interface IViewFor<CursorViewModel> with
        member this.ViewModel
            with get (): CursorViewModel = this.GetValue(ViewModelProp)
            and set (v: CursorViewModel): unit = this.SetValue(ViewModelProp, v)
        member this.ViewModel
            with get (): obj = this.GetValue(ViewModelProp) :> obj
            and set (v: obj): unit = this.SetValue(ViewModelProp, v)

