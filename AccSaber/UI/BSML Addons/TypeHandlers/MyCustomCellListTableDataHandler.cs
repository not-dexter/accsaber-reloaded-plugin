using AccsaberLeaderboard.UI.BSML_Addons.Components;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.TypeHandlers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AccsaberLeaderboard.UI.BSML_Addons.TypeHandlers
{
    [ComponentHandler(typeof(MyCustomCellListTableData))]
    internal class MyCustomCellListTableDataHandler : TypeHandler
    {
        public override Dictionary<string, string[]> Props => new()
        {
            { "selectCell", [ "select-cell" ] },
            { "data", [ "contents", "data" ] },
            { "cellClickable", [ "clickable-cells" ] },
            { "cellNumber", [ "pref-number-cells", "cells", "number-of-cells", "visible-cells" ] },
            { "cellSize", [ "main-cell-size", "cell-size" ] }
        };

        public override void HandleType(BSMLParser.ComponentTypeWithData componentType, BSMLParserParams parserParams)
        {
#if NEW_VERSION
            ref Component component = ref componentType.Component;
            ref Dictionary<string, string> data = ref componentType.Data;
            Dictionary<string, BSMLValue> values = parserParams.Values;
            Dictionary<string, BSMLAction> actions = parserParams.Actions;
#else
            ref Component component = ref componentType.component;
            ref Dictionary<string, string> data = ref componentType.data;
            Dictionary<string, BSMLValue> values = parserParams.values;
            Dictionary<string, BSMLAction> actions = parserParams.actions;
#endif

            MyCustomCellListTableData componentData = (component as MyCustomCellListTableData)!;

            if (data.TryGetValue("selectCell", out string selectCell))
            {
                componentData.OnCellClick += index =>
                {
                    if (!actions.TryGetValue(selectCell, out BSMLAction action))
                    {
                        throw new Exception("select-cell action '" + selectCell + "' not found");
                    }

                    action.Invoke(componentData.Data[index]);
                };
            }

            if (data.TryGetValue("data", out string dataStr))
            {
                if (!values.TryGetValue(dataStr, out BSMLValue contents))
                    throw new Exception("Value '" + dataStr + "' not found");

                object maybeCells = contents.GetValue();

                if (maybeCells is not IEnumerable ienum)
                    throw new Exception($"Value '{dataStr}' is not an IEnumerable and cannot be used as data for my-custom-list.");

                componentData.Data = [.. ConvariantConverter<ICellDataSource>(ienum)];
            }

            if (data.TryGetValue("cellClickable", out string cellClickable))
                componentData.ClickableCells = Parse.Bool(cellClickable);

            if (data.TryGetValue("cellNumber", out string cellNum))
            {
                if (!int.TryParse(cellNum, out int value))
                    throw new Exception($"the cell number \"{cellNum}\" cannot be parsed into an int.");

                componentData.PrefNumberOfCells = value;
            }

            if (data.TryGetValue("cellSize", out string cellSize))
            {
                if (!float.TryParse(cellSize, out float value))
                    throw new Exception($"the cell size \"{cellSize}\" cannot be parsed into a float.");

                componentData.MainCellSize = value;
            }
        }

        public static IEnumerable<T2> ConvariantConverter<T2>(IEnumerable arr)
        {
            if (arr is IEnumerable<T2> typedEnumerable)
                return typedEnumerable;

            // Try to find an implemented IEnumerable<T> and check T
            Type? enumerableInterface = arr.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableInterface is not null)
            {
                if (typeof(T2).IsAssignableFrom(enumerableInterface.GetGenericArguments()[0]))
                    // Safe to cast each item to T2 at runtime
                    return arr.Cast<T2>();
                else
                    throw new Exception($"Value '{arr}' implements IEnumerable<{enumerableInterface.GetGenericArguments()[0].Name}> which is not assignable to IEnumerable<{typeof(T2)}>.");
            }
            else
            {
                // Fallback: enumerate once and verify each element implements T2
                List<T2> list = [];
                foreach (object? item in arr)
                {
                    if (item is T2 type2)
                        list.Add(type2);
                    else
                        throw new Exception($"Value '{arr}' contains an element of type {item?.GetType().Name ?? "null"} which does not implement {typeof(T2)}.");
                }
                return list;
            }
        }
    }
}
