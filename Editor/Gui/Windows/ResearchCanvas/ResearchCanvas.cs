﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using T3.Editor.Gui.Graph.Interaction;
using T3.Editor.Gui.InputUi;
using T3.Editor.Gui.Interaction;
using T3.Editor.Gui.Selection;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.Windows.ResearchCanvas;

public class ResearchWindow : Window
{
    public ResearchWindow()
    {
        Config.Title = "Research";
        MenuTitle = "Research";
        WindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    protected override void DrawContent()
    {
        DrawWindowContent();
    }

    private bool _initialized;
    private Vector2 _dampedMovePos;

    private void DrawWindowContent(bool hideHeader = false)
    {
        if (true || !_initialized)
        {
            InitializeFromSymbol();
            InitializeFromDefinition();
        }

        var drawList = ImGui.GetWindowDrawList();
        var keepCursorPos = ImGui.GetCursorScreenPos();

        // move test block
        var posOnCanvas = _canvas.InverseTransformPositionFloat(ImGui.GetMousePos());
        _movingTestBlock.PosOnCanvas = posOnCanvas;

        // snap test block
        var foundSnapPos = false;
        var bestSnapDistance = float.PositiveInfinity;
        var bestSnapPos = Vector2.Zero;
        var snapThreshold = 20;
        

        foreach (var movingBlockSlot in _movingTestBlock.GetSlots())
        {
            var slotPosA = movingBlockSlot.Block.PosOnCanvas + movingBlockSlot.AnchorPos * BlockSize;
            
            foreach (var other in _blocks)
            {
                if (other == _movingTestBlock)
                    continue;

                var otherSlots = movingBlockSlot.IsInput ? other.Outputs : other.Inputs;
                foreach (var otherSlot in otherSlots)
                {
                    var otherSlotPos = other.PosOnCanvas + otherSlot.AnchorPos * BlockSize;
                    var delta = slotPosA - otherSlotPos;
                    var distance = delta.Length();
                    if (distance > snapThreshold)
                        continue;

                    if (distance < bestSnapDistance)
                    {
                        bestSnapDistance = distance;
                        bestSnapPos = other.PosOnCanvas -  (movingBlockSlot.AnchorPos - otherSlot.AnchorPos) * BlockSize;
                        foundSnapPos = true;
                    }
                }
            }
        }

        if (foundSnapPos)
        {
            _dampedMovePos = Vector2.Lerp(_dampedMovePos,bestSnapPos,0.5f);
            _movingTestBlock.PosOnCanvas = _dampedMovePos;
        }
        else
        {
            _dampedMovePos = _movingTestBlock.PosOnCanvas;
            //_movingTestBlock.PosOnCanvas = posOnCanvas;
        }

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        {
            _canvas.UpdateCanvas();
            HandleFenceSelection();

            var anchorScale = _canvas.TransformDirection(BlockSize);
            var slotSize = 3 * _canvas.Scale.X;

            foreach (var b in _blocks)
            {
                if (!TypeUiRegistry.Entries.TryGetValue(b.PrimaryType, out var typeUiProperties))
                    continue;

                var c = typeUiProperties.Color;
                var cLabel = ColorVariations.OperatorLabel.Apply(c);

                var pMin = _canvas.TransformPosition(b.PosOnCanvas);
                var pMax = _canvas.TransformPosition(b.PosOnCanvas + BlockSize);
                drawList.AddRectFilled(pMin, pMax, c.Fade(0.7f), 3);
                drawList.AddRect(pMin, pMax, UiColors.BackgroundFull.Fade(0.6f), 3);
                drawList.AddText(pMin + new Vector2(2, -2), cLabel, b.Name);

                foreach (var slot in b.GetSlots())
                {
                    var xxx = slot.Connections.Count > 0 && slot.Connections[0].IsSnapped;
                    var thickness = xxx ? 2 : 1;
                    drawList.AddCircle(pMin + anchorScale * slot.AnchorPos, slotSize, c, 3, thickness);
                }
            }
        }

        drawList.ChannelsSetCurrent(0);
        {
            ImGui.SetCursorScreenPos(keepCursorPos);
        }

        drawList.ChannelsMerge();
    }

    private readonly ScalableCanvas _canvas = new();

    public void Draw(ImDrawListPtr drawList, bool hideHeader = false)
    {
        _canvas.UpdateCanvas();
        HandleFenceSelection();
    }

    private void HandleFenceSelection()
    {
        _fenceState = SelectionFence.UpdateAndDraw(_fenceState);
        switch (_fenceState)
        {
            case SelectionFence.States.PressedButNotMoved:
                if (SelectionFence.SelectMode == SelectionFence.SelectModes.Replace)
                    _selection.Clear();
                break;

            case SelectionFence.States.Updated:
                HandleSelectionFenceUpdate(SelectionFence.BoundsInScreen);
                break;

            case SelectionFence.States.CompletedAsClick:
                _selection.Clear();
                break;
        }
    }

    private void HandleSelectionFenceUpdate(ImRect boundsInScreen)
    {
        var boundsInCanvas = _canvas.InverseTransformRect(boundsInScreen);
        // var elementsToSelect = (from child in PoolForBlendOperations.Variations
        //                         let rect = new ImRect(child.PosOnCanvas, child.PosOnCanvas + child.Size)
        //                         where rect.Overlaps(boundsInCanvas)
        //                         select child).ToList();

        _selection.Clear();
        // foreach (var element in elementsToSelect)
        // {
        //     Selection.AddSelection(element);
        // }
    }

    /// <summary>
    /// Implement selectionContainer
    /// </summary>
    public IEnumerable<ISelectableCanvasObject> GetSelectables()
    {
        //return PoolForBlendOperations.Variations;
        return new List<ISelectableCanvasObject>();
    }

    private SelectionFence.States _fenceState;
    private readonly CanvasElementSelection _selection = new();

    public override List<Window> GetInstances()
    {
        return new List<Window>();
    }

    private void InitializeFromSymbol()
    {
        _blocks.Clear();
        _connections.Clear();
        _groups.Clear();
        _slots.Clear();

        var symbol = SymbolUiRegistry.Entries.Values.FirstOrDefault(c => c.Symbol.Name == "CCCampTable");
        if (symbol == null)
            return;

        foreach (var child in symbol.ChildUis)
        {
            _blocks.Add(new Block
                            {
                                PosOnCanvas = child.PosOnCanvas,
                                UnitHeight = 1,
                                Name = child.SymbolChild.ReadableName,
                            });
        }

        _initialized = true;
    }

    public static readonly Vector2 BlockSize = new(100, 20);

    private void InitializeFromDefinition()
    {
        _blocks.Clear();
        _connections.Clear();
        _groups.Clear();
        _slots.Clear();

        var a = new Block(0, 1, "a");
        var b = new Block(0, 2, "b");
        var c = new Block(0, 3, "c");

        var d = new Block(2, 6, "d");

        _blocks.Add(a);
        _blocks.Add(b);
        _blocks.Add(c);
        _blocks.Add(d);

        _movingTestBlock = d;

        _connections.Add(new Connection() { Source = a.Outputs[0], Target = b.Inputs[0], IsSnapped = true });
        _connections.Add(new Connection() { Source = b.Outputs[0], Target = c.Inputs[0], IsSnapped = true });

        // Register connections to blocks
        foreach (var connection in _connections)
        {
            connection.Source?.Block.Outputs[0].Connections.Add(connection);
            connection.Target?.Block.Inputs[0].Connections.Add(connection);
        }

        _initialized = true;
    }

    private Block _movingTestBlock;
    private readonly List<Block> _blocks = new();
    private readonly List<Connection> _connections = new();
    private readonly List<Group> _groups = new();
    private readonly List<Slot> _slots = new();
}

public class Block
{
    public Block()
    {
    }

    public Block(int x, int y, string name)
    {
        Name = name;
        PosOnCanvas = new Vector2(x, y) * ResearchWindow.BlockSize;
        Inputs = new List<Slot>()
                     {
                         new() { Block = this, AnchorPos = new Vector2(0.5f, 0.0f), IsInput = true },
                         new() { Block = this, AnchorPos = new Vector2(0.0f, 0.5f), IsInput = true },
                     };

        Outputs = new List<Slot>()
                      {
                          new() { Block = this, AnchorPos = new Vector2(0.5f, 1.0f) },
                          new() { Block = this, AnchorPos = new Vector2(1f, 0.5f) },
                      };
    }

    public IEnumerable<Slot> GetSlots()
    {
        foreach (var input in Inputs)
        {
            yield return input;
        }

        foreach (var output in Outputs)
        {
            yield return output;
        }
    }

    public Vector2 PosOnCanvas;
    public int UnitHeight;
    public string Name;
    public Type PrimaryType = typeof(float);

    public List<Slot> Inputs = new();
    public List<Slot> Outputs = new();
}

public class Group
{
    public List<Block> Blocks;
}

public class Slot
{
    public Block Block;
    public Type Type = typeof(float);
    public Vector2 AnchorPos;
    public bool IsInput;
    public List<Connection> Connections = new List<Connection>(); // not sure if merging inout and output connection is a good idea.
    public bool IsShy;

    public enum Visibility
    {
        Visible,
        ShyButConnected,
        ShyButRevealed,
        Shy,
    }
}

public class Connection
{
    public Slot Source;
    public Slot Target;
    public bool IsSnapped;
}