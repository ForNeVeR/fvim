﻿module FVim.ui

open wcwidth
open FVim.neovim.def
open Avalonia.Input
open Avalonia.Media
open System.Runtime.InteropServices
open Avalonia.Platform
open Avalonia
open SkiaSharp
open Avalonia.Skia
open System.Reflection
open System

type InputEvent = 
| Key          of mods: InputModifiers * key: Key
| MousePress   of mods: InputModifiers * row: int * col: int * button: MouseButton * combo: int
| MouseRelease of mods: InputModifiers * row: int * col: int * button: MouseButton
| MouseDrag    of mods: InputModifiers * row: int * col: int * button: MouseButton
| MouseWheel   of mods: InputModifiers * row: int * col: int * dx: int * dy: int
| TextInput    of text: string

type IGridUI =
    abstract Id: int
    abstract Connect: IEvent<RedrawCommand[]> -> IEvent<int> -> unit
    abstract GridHeight: int
    abstract GridWidth: int
    abstract Resized: IEvent<IGridUI>
    abstract Input: IEvent<InputEvent>

[<Struct>]
type CursorInfo =
    {
        enabled: bool
        typeface: string
        wtypeface: string
        fontSize: float
        text: string
        fg: Color
        bg: Color
        sp: Color
        underline: bool
        undercurl: bool
        bold: bool
        italic: bool
        blinkon: int
        blinkoff: int
        blinkwait: int
        cellPercentage: int
        shape: CursorShape
        h: float
        w: float
        x: float
        y: float
    }
    with static member Default = 
        { 
            enabled = true
            typeface = ""
            wtypeface = ""
            fontSize = 1.0
            text = ""
            fg = Colors.White
            bg = Colors.Black
            sp = Colors.Red
            underline = false
            undercurl = false
            bold = false
            italic = false
            blinkon = 0
            blinkoff = 0
            blinkwait = 0
            cellPercentage = 100
            shape = CursorShape.Block
            w = 1.0
            h = 1.0
            x = 0.0
            y = 0.0
        }

let private nerd_typeface = SKTypeface.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("fvim.nerd.ttf"))
let private fontcache = System.Collections.Generic.Dictionary<string*bool*bool, SKTypeface>()

let GetTypeface(txt, italic, bold, font, wfont) =
    let w = wswidth txt

    let _get fname =
        match fontcache.TryGetValue((fname, italic, bold)) with
        | true, typeface -> typeface
        | _ ->
            let weight   = if bold then SKFontStyleWeight.Medium else SKFontStyleWeight.Normal
            let width    = SKFontStyleWidth.Normal
            let slang    = if italic then SKFontStyleSlant.Italic else SKFontStyleSlant.Upright
            let typeface = SKTypeface.FromFamilyName(fname, weight, width, slang)
            fontcache.[(fname, italic, bold)] <- typeface
            typeface

    let wfont = if String.IsNullOrEmpty wfont then font else wfont

    match w with
    | CharType.Wide -> _get wfont
    | CharType.Nerd -> nerd_typeface
    | _             -> _get font


let GetForegroundBrush(c: Color, fontFace: SKTypeface, fontSize: float) =
    let paint                   = new SKPaint(Color = c.ToSKColor())
    paint.Typeface             <- fontFace
    paint.TextSize             <- single fontSize
    paint.IsAntialias          <- true
    paint.IsAutohinted         <- true
    paint.IsLinearText         <- false
    paint.HintingLevel         <- SKPaintHinting.Full
    paint.LcdRenderText        <- true
    paint.SubpixelText         <- true
    paint.TextAlign            <- SKTextAlign.Left
    paint.DeviceKerningEnabled <- false
    paint.TextEncoding         <- SKTextEncoding.Utf16
    paint

let RenderText (ctx: IDrawingContextImpl, region: Rect, fg: SKPaint, bg: SKPaint, sp: SKPaint, underline: bool, undercurl: bool, text: string) =
    //  DrawText accepts the coordinate of the baseline.
    //  h = [padding space 1] + above baseline | below baseline + [padding space 2]
    let h = region.Bottom - region.Y
    //  total_padding = padding space 1 + padding space 2
    let total_padding = h + float fg.FontMetrics.Top - float fg.FontMetrics.Bottom
    let baseline      = region.Y - float fg.FontMetrics.Top + (total_padding / 2.8)
    let fontPos       = Point(region.X, baseline)

    let skia = ctx :?> DrawingContextImpl

    skia.Canvas.DrawRect(region.ToSKRect(), bg)
    skia.Canvas.DrawText(text, fontPos.ToSKPoint(), fg)

    // Text bounding box drawing:
    // --------------------------------------------------
    // let bounds = ref <| SKRect()
    // ignore <| fg.MeasureText(String.Concat str, bounds)
    // let mutable bounds = !bounds
    // bounds.Left <- bounds.Left + single (fontPos.X)
    // bounds.Top <- bounds.Top + single (fontPos.Y)
    // bounds.Right <- bounds.Right + single (fontPos.X)
    // bounds.Bottom <- bounds.Bottom + single (fontPos.Y)
    // fg.Style <- SKPaintStyle.Stroke
    // skia.Canvas.DrawRect(bounds, fg)
    // --------------------------------------------------

    if underline then
        let underline_pos = fg.FontMetrics.UnderlinePosition.GetValueOrDefault()
        let p1 = fontPos + Point(0.0, float <| underline_pos)
        let p2 = p1 + Point(region.Width, 0.0)
        sp.Style <- SKPaintStyle.Stroke
        skia.Canvas.DrawLine(p1.ToSKPoint(), p2.ToSKPoint(), sp)

    if undercurl then
        let underline_pos  = fg.FontMetrics.UnderlinePosition.GetValueOrDefault()
        let mutable px, py = single fontPos.X, single fontPos.Y 
        py <- py + underline_pos
        let qf             = 0.5F
        let hf             = qf * 2.0F
        let q3f            = qf * 3.0F
        let ff             = qf * 4.0F
        let r              = single region.Right
        let py1            = py - 2.0f
        let py2            = py + 2.0f
        sp.Style <- SKPaintStyle.Stroke
        use path = new SKPath()
        path.MoveTo(px, py)
        while px < r do
            path.LineTo(px,       py)
            path.LineTo(px + qf,  py1)
            path.LineTo(px + hf,  py)
            path.LineTo(px + q3f, py2)
            px <- px + ff
        skia.Canvas.DrawPath(path , sp)
