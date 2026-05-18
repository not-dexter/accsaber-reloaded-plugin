using AccSaber.Utils;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.BSML_Addons.Components
{
    public class MyCustomCellListTableData : MonoBehaviour
    {
        private readonly List<string> cellTemplates = [];
        private readonly List<float> cellSizes = [];
        private readonly List<MyCustomCell> dataSources = [];

        private MyCustomCell? previouslySelected = null;
        private List<ICellDataSource> data = [];
        private bool clickableCells = false;
        private int prefNumberOfCells = 10;
        private float mainCellSize = 8.5f;
        private float initialAnchorY = -1f;

        public event Action<int> OnCellClick, OnCellHighlighted, OnCellUnhighlighted;
        public List<string> CellTemplates => cellTemplates;
        public List <float> CellSizes => cellSizes;
        public List<ICellDataSource> Data
        {
            get => data;
            set { data = value; ReloadTemplates(); }
        }
        public bool ClickableCells
        {
            get => clickableCells;
            set => clickableCells = value;
        }
        public int PrefNumberOfCells
        {
            get => prefNumberOfCells;
            set => prefNumberOfCells = value;
        }
        public float MainCellSize
        {
            get => mainCellSize;
            set => mainCellSize = value;
        }

        public MyCustomCellListTableData()
        {
            OnCellClick += UpdateSelected;
            OnCellHighlighted += index => { dataSources[index].highlighted = true; dataSources[index].RefreshVisuals(); };
            OnCellUnhighlighted += index => { dataSources[index].highlighted = false; dataSources[index].RefreshVisuals(); };
        }

        public int NumberOfCells() => data.Count;
        public float CellSize(int idx) => cellSizes.Count > 0 ? cellSizes[data[idx].TemplateId] : mainCellSize;
        public MyCustomCell CellForIdx(int idx)
        {
            GameObject go = new("Cell", typeof(RectTransform));
            MyCustomCell cell = go.AddComponent<MyCustomCell>();

            if (clickableCells)
            {
                MyEventSystemListener listener = cell.gameObject.AddComponent<MyEventSystemListener>();
                int capturedIndex = idx;

                listener.pointerDidClickEvent += _ => OnCellClick?.Invoke(capturedIndex);
                listener.pointerDidEnterEvent += _ => OnCellHighlighted?.Invoke(capturedIndex);
                listener.pointerDidExitEvent += _ => OnCellUnhighlighted?.Invoke(capturedIndex);
            }

            cell.name = "MyCustomTableCell";
            int tempId = data[idx].TemplateId;
            cell.ParserParams = VersionUtils.BSMLParser_Instance.Parse(cellTemplates[tempId], cell.gameObject, data[idx]);
            cell.SetupPostParse();

            foreach (var g in cell.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

            cell.GetComponent<Touchable>().raycastTarget = true;

            cell.GetComponent<LayoutElement>().preferredHeight = cellSizes[tempId];
            return cell;
        }
        
        private void ReloadTemplates()
        {
            if (initialAnchorY < 0)
                initialAnchorY = gameObject.GetComponent<RectTransform>().anchoredPosition.y;

            cellTemplates.Clear();
            cellSizes.Clear();

            foreach (MyCustomCell cell in dataSources)
                Destroy(cell.gameObject);

            dataSources.Clear();

            Dictionary<string, int> paths = [];
            Assembly current = Assembly.GetExecutingAssembly();
            int cellId = 0;
            float cellHeight = 0f;

            data = [.. data.Where(cell => cell is not null)];

            foreach (ICellDataSource cell in data)
            {
                if (paths.TryGetValue(cell.TemplatePath, out int id))
                    cell.TemplateId = id;
                else
                {
                    id = paths.Count;
                    paths.Add(cell.TemplatePath, id);
                    cellTemplates.Add(cell.TemplatePath.First() == '<' ? cell.TemplatePath : Utilities.GetResourceContent(current, cell.TemplatePath));
                    cellSizes.Add(cell.CellSize);
                    cell.TemplateId = id;
                }

                MyCustomCell customCell = CellForIdx(cellId++);
                customCell.transform.SetParent(transform, false);

                cellHeight += cellSizes[id];

                dataSources.Add(customCell);
            }

            LayoutElement le = gameObject.GetComponent<LayoutElement>();
            le.preferredHeight = cellHeight;
            le.minHeight = cellHeight;
            
            RectTransform rt = gameObject.GetComponent<RectTransform>();
            Vector2 anchorPos = rt.anchoredPosition;
            anchorPos.y = initialAnchorY;
            if (data.Count < PrefNumberOfCells)
                anchorPos.y += mainCellSize / 2f * (PrefNumberOfCells - data.Count);
            rt.anchoredPosition = anchorPos;

            Canvas.ForceUpdateCanvases();
        }
        private void UpdateSelected(int index)
        {
            previouslySelected?.selected = false;

            previouslySelected = dataSources[index];
            previouslySelected.selected = true;

            previouslySelected.RefreshVisuals();
        }


    }

    public class MyCustomCell : MonoBehaviour
    {
        internal bool selected, highlighted;
        public BSMLParserParams? ParserParams { get; internal set; }

        public List<GameObject> selectedTags = null!;

        public List<GameObject> hoveredTags = null!;

        public List<GameObject> neitherTags = null!;

        public virtual void RefreshVisuals()
        {
            foreach (GameObject selectedTag in selectedTags)
            {
                selectedTag.SetActive(selected);
            }

            foreach (GameObject hoveredTag in hoveredTags)
            {
                hoveredTag.SetActive(highlighted);
            }

            foreach (GameObject neitherTag in neitherTags)
            {
                neitherTag.SetActive(!selected && !highlighted);
            }
#if NEW_VERSION
            if (ParserParams?.Actions.TryGetValue("refresh-visuals", out BSMLAction? value) ?? false)
#else
            if (ParserParams?.actions.TryGetValue("refresh-visuals", out BSMLAction? value) ?? false)
#endif
            {
                value.Invoke(selected, highlighted);
            }
        }
        protected internal void SetupPostParse()
        {
            if (ParserParams is null)
                throw new InvalidOperationException("ParserParams cannot be null when calling SetupPostParse.");

            selectedTags = ParserParams.GetObjectsWithTag("selected");
            hoveredTags = ParserParams.GetObjectsWithTag("hovered");
            neitherTags = ParserParams.GetObjectsWithTag("un-selected-un-hovered");
        }
        private void Awake()
        {
            RectTransform rt = (gameObject.transform as RectTransform)!;

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 8.5f);

            gameObject.AddComponent<LayoutElement>();
            gameObject.AddComponent<Touchable>();
        }
    }

    public interface ICellDataSource
    {
        public abstract string TemplatePath { get; }
        public abstract float CellSize { get; }
        public int TemplateId { get; set; }
    }
}
