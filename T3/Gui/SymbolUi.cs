using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using T3.Core;
using T3.Core.Logging;
using T3.Core.Operator;
using T3.Gui.Graph;
using T3.Gui.Graph.Interaction;
using T3.Gui.InputUi;
using T3.Gui.OutputUi;
using T3.Gui.Selection;

namespace T3.Gui
{
    public class SymbolUi
    {
        public Symbol Symbol { get; }

        public SymbolUi(Symbol symbol)
        {
            Symbol = symbol;
            UpdateConsistencyWithSymbol(); // this sets up all missing elements
        }

        public SymbolUi(Symbol symbol, List<SymbolChildUi> childUis, Dictionary<Guid, IInputUi> inputs, Dictionary<Guid, IOutputUi> outputs)
        {
            Symbol = symbol;
            ChildUis = childUis;
            InputUis = inputs;
            OutputUis = outputs;

            UpdateConsistencyWithSymbol();
        }

        public SymbolUi CloneForNewSymbol(Symbol newSymbol, Dictionary<Guid, Guid> oldToNewIds)
        {
            var childUis = new List<SymbolChildUi>(ChildUis.Count);
            // foreach (var sourceChildUi in ChildUis)
            // {
            //     var clonedChildUi = sourceChildUi.Clone();
            //     Guid newChildId = oldToNewIds[clonedChildUi.Id];
            //     clonedChildUi.SymbolChild = newSymbol.Children.Single(child => child.Id == newChildId);
            //     childUis.Add(clonedChildUi);
            // }

            var inputUis = new Dictionary<Guid, IInputUi>(InputUis.Count);
            foreach (var (_, inputUi) in InputUis)
            {
                var clonedInputUi = inputUi.Clone();
                clonedInputUi.Parent = this;
                Guid newInputId = oldToNewIds[clonedInputUi.Id];
                clonedInputUi.InputDefinition = newSymbol.InputDefinitions.Single(inputDef => inputDef.Id == newInputId);
                inputUis.Add(clonedInputUi.Id, clonedInputUi);
            }

            var outputUis = new Dictionary<Guid, IOutputUi>(OutputUis.Count);
            foreach (var (_, outputUi) in OutputUis)
            {
                var clonedOutputUi = outputUi.Clone();
                Guid newOutputId = oldToNewIds[clonedOutputUi.Id];
                clonedOutputUi.OutputDefinition = newSymbol.OutputDefinitions.Single(outputDef => outputDef.Id == newOutputId);
                outputUis.Add(clonedOutputUi.Id, clonedOutputUi);
            }
            
            return new SymbolUi(newSymbol, childUis, inputUis, outputUis);
        }
        

        public IEnumerable<ISelectableNode> GetSelectables()
        {
            foreach (var childUi in ChildUis)
                yield return childUi;

            foreach (var inputUi in InputUis)
                yield return inputUi.Value;

            foreach (var outputUi in OutputUis)
                yield return outputUi.Value;
        }

        public void UpdateConsistencyWithSymbol()
        {
            // check if child entries are missing
            foreach (var child in Symbol.Children)
            {
                if (!ChildUis.Exists(c => c.Id == child.Id))
                {
                    Log.Debug($"Found no symbol child ui entry for symbol child '{child.ReadableName}' - creating a new one");
                    var childUi = new SymbolChildUi()
                                  {
                                      SymbolChild = child,
                                      PosOnCanvas = new Vector2(100, 100)
                                  };
                    ChildUis.Add(childUi);
                }
            }

            // check if there are child entries where no symbol child exists anymore
            ChildUis.RemoveAll(childUi => !Symbol.Children.Exists(child => child.Id == childUi.Id));

            // check if input UIs are missing
            var inputUiFactory = InputUiFactory.Entries;
            for (int i = 0; i < Symbol.InputDefinitions.Count; i++)
            {
                Symbol.InputDefinition input = Symbol.InputDefinitions[i];
                if (!InputUis.TryGetValue(input.Id, out var existingInputUi) || existingInputUi.Type != input.DefaultValue.ValueType)
                {
                    Log.Debug($"Found no input ui entry for symbol child input '{input.Name}' - creating a new one");
                    InputUis.Remove(input.Id);
                    var inputCreator = inputUiFactory[input.DefaultValue.ValueType];
                    IInputUi newInputUi = inputCreator();
                    newInputUi.Parent = this;
                    newInputUi.InputDefinition = input;
                    newInputUi.PosOnCanvas = GetCanvasPositionForNextInputUi(this);
                    InputUis.Add(input.Id, newInputUi);
                }
                else
                {
                    existingInputUi.Parent = this;
                }
            }

            // check if there are input entries where no input ui exists anymore
            foreach (var inputUiToRemove in InputUis.Where(kv => !Symbol.InputDefinitions.Exists(inputDef => inputDef.Id == kv.Key)).ToList())
            {
                Log.Debug($"InputUi '{inputUiToRemove.Value.Id}' still existed but no corresponding input definition anymore. Removing the ui.");
                InputUis.Remove(inputUiToRemove.Key);
            }

            var outputUiFactory = OutputUiFactory.Entries;
            foreach (var output in Symbol.OutputDefinitions)
            {
                if (!OutputUis.TryGetValue(output.Id, out var value) || (value.Type != output.ValueType))
                {
                    Log.Debug($"Found no output ui entry for symbol child output '{output.Name}' - creating a new one");
                    OutputUis.Remove(output.Id);
                    var outputUiCreator = outputUiFactory[output.ValueType];
                    var newOutputUi = outputUiCreator();
                    newOutputUi.OutputDefinition = output;
                    OutputUis.Add(output.Id, newOutputUi);
                }
            }

            // check if there are input entries where no output ui exists anymore
            foreach (var outputUiToRemove in OutputUis.Where(kv => !Symbol.OutputDefinitions.Exists(outputDef => outputDef.Id == kv.Key)).ToList())
            {
                Log.Debug($"OutputUi '{outputUiToRemove.Value.Id}' still existed but no corresponding input definition anymore. Removing the ui.");
                OutputUis.Remove(outputUiToRemove.Key);
            }
        }

        private Vector2 GetCanvasPositionForNextInputUi(SymbolUi symbolUi)
        {
            if (symbolUi.Symbol.InputDefinitions.Count == 0)
            {
                return new Vector2(-200, 0);
            }

            IInputUi lastInputUi = null;

            foreach (var inputDef in symbolUi.Symbol.InputDefinitions)
            {
                if (symbolUi.InputUis.ContainsKey(inputDef.Id))
                    lastInputUi = symbolUi.InputUis[inputDef.Id];
            }

            if (lastInputUi == null)
                return new Vector2(-200, 0);

            return lastInputUi.PosOnCanvas + new Vector2(0, lastInputUi.Size.Y + SelectableNodeMovement.SnapPadding.Y);
        }

        public Guid AddChild(Symbol symbolToAdd, Guid addedChildId, Vector2 posInCanvas, Vector2 size)
        {
            Symbol.AddChild(symbolToAdd, addedChildId);
            var childUi = new SymbolChildUi
                          {
                              SymbolChild = Symbol.Children.Find(entry => entry.Id == addedChildId),
                              PosOnCanvas = posInCanvas,
                              Size = size,
                          };
            ChildUis.Add(childUi);

            return addedChildId;
        }
        
        public Guid AddChildAsCopyFromSource(Symbol symbolToAdd, Guid addedChildId, SymbolUi sourceCompositionSymbolUi, Guid sourceChildId, Vector2 posInCanvas)
        {
            Symbol.AddChild(symbolToAdd, addedChildId);
            var sourceChildUi = sourceCompositionSymbolUi.ChildUis.Single(child => child.Id == sourceChildId);
            var childUi = sourceChildUi.Clone();
            childUi.SymbolChild = Symbol.Children.Find(entry => entry.Id == addedChildId);
            childUi.PosOnCanvas = posInCanvas;
            ChildUis.Add(childUi);

            return addedChildId;
        }

        public void RemoveChild(Guid id)
        {
            Symbol.RemoveChild(id); // remove from symbol

            // now remove ui entry
            var childToRemove = ChildUis.Single(child => child.Id == id);
            ChildUis.Remove(childToRemove);
        }

        public string Description { get; set; }
        // public Styles DefaultStyleForInstances { get; set; }  // TODO: Implement inheritance for display styles? 
        public List<SymbolChildUi> ChildUis = new List<SymbolChildUi>();    // TODO: having this as dictionary with instanceIds would simplify drawing the graph 
        public Dictionary<Guid, IInputUi> InputUis { get; } = new Dictionary<Guid, IInputUi>();
        public Dictionary<Guid, IOutputUi> OutputUis { get; }= new Dictionary<Guid, IOutputUi>();
    }

    public static class SymbolUiRegistry
    {
        public static Dictionary<Guid, SymbolUi> Entries { get; } = new Dictionary<Guid, SymbolUi>(20);
    }
}