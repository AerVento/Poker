using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed partial class DailyTaskPanel
    {
        private RectTransform _taskList;
        private TaskCardScrollRect _scroll;
        private TMPro.TextMeshProUGUI _pageLabel;
        private Button _previousCard;
        private Button _nextCard;
        private string _visibleInstanceId;

        // Reuse the authored card skin and fonts; the list owns only layout and card instances.
        private void EnsureScrollView()
        {
            if (_scroll != null) return;
            var viewport = new GameObject("TaskScrollView", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(TaskCardScrollRect));
            var rect = (RectTransform)viewport.transform;
            rect.SetParent(_content, false);
            rect.sizeDelta = new Vector2(640, 560);
            rect.anchoredPosition = new Vector2(0, -25);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
            _taskList = (RectTransform)new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter)).transform;
            _taskList.SetParent(rect, false);
            _taskList.anchorMin = new Vector2(0, 0);
            _taskList.anchorMax = new Vector2(0, 1);
            _taskList.pivot = new Vector2(0, 0.5f);
            _taskList.sizeDelta = Vector2.zero;
            var layout = _taskList.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 64;
            layout.padding = new RectOffset(0, 0, 20, 20);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            _taskList.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var card in _cards) card.transform.SetParent(_taskList, false);
            _scroll = viewport.GetComponent<TaskCardScrollRect>();
            _scroll.viewport = rect;
            _scroll.content = _taskList;
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40;
            _scroll.inertia = false;
            _pageLabel = Instantiate(_resetTime, _content);
            _pageLabel.name = "TaskPageNumber";
            _pageLabel.rectTransform.anchoredPosition = new Vector2(0, -320);
            _pageLabel.rectTransform.sizeDelta = new Vector2(300, 36);
            _pageLabel.alignment = TMPro.TextAlignmentOptions.Center;
            _pageLabel.fontSize = 22;
            _pageLabel.raycastTarget = false;
            _feedback.rectTransform.anchoredPosition = new Vector2(0, -371);
            _previousCard = MakePageButton("PreviousTask", "‹", -412);
            _nextCard = MakePageButton("NextTask", "›", 412);
            _previousCard.onClick.AddListener(() => _scroll.SelectPage(_scroll.CurrentIndex - 1));
            _nextCard.onClick.AddListener(() => _scroll.SelectPage(_scroll.CurrentIndex + 1));
            _scroll.PageChanged += (index, count) =>
            {
                _pageLabel.text = count == 0 ? "暂无任务" : $"{index + 1} / {count}";
                _previousCard.interactable = index > 0;
                _nextCard.interactable = index + 1 < count;
                if (_snapshot.Tasks != null && index < _snapshot.Tasks.Length)
                    _visibleInstanceId = _snapshot.Tasks[index].InstanceId;
            };
            var subtitle = _content.Find("Subtitle");
            if (subtitle != null && subtitle.TryGetComponent<TMPro.TextMeshProUGUI>(out var text))
                text.text = "左右滑动切换任务 · 完成目标后领取奖励";
        }

        private Button MakePageButton(string name, string label, float x)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(_content, false);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(72, 96);
            rect.anchoredPosition = new Vector2(x, -25);
            button.targetGraphic = button.GetComponent<Image>();
            button.targetGraphic.color = new Color(0.13f, 0.23f, 0.19f, 0.9f);
            var text = Instantiate(_resetTime, button.transform);
            text.text = label;
            text.fontSize = 48;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.raycastTarget = false;
            return button;
        }

        private void EnsureCardCount(int count)
        {
            if (count <= _cards.Length) return;
            var oldCount = _cards.Length;
            Array.Resize(ref _cards, count);
            for (var index = oldCount; index < count; index++)
            {
                _cards[index] = Instantiate(_cards[0], _taskList);
                _cards[index].name = "TaskCard";
                _cards[index].InstanceClaimClicked += Claim;
                _cards[index].DeleteClicked += ConfirmDelete;
            }
        }
    }
}
