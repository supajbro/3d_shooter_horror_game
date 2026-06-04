using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : StateUI
{
    [SerializeField] private GenericButton m_playGameButton;
    [SerializeField] private GenericButton m_levelSelectButton;
    [SerializeField] private GenericButton m_settingsButton;
    [SerializeField] private GenericButton m_quitButton;

    [SerializeField] private string m_sceneName;

    [Header("Intro Animation")]
    [SerializeField] private float m_staggerDelay = 0.12f;
    [SerializeField] private float m_moveDuration = 0.6f;
    [SerializeField] private float m_fadeDuration = 0.4f;
    [SerializeField] private float m_yOffset = 80f;
    [SerializeField] private LeanTweenType m_ease = LeanTweenType.easeOutCubic;

    private GenericButton[] m_buttons;

    private RectTransform[] m_rects;
    private CanvasGroup[] m_canvasGroups;
    private Vector2[] m_finalPositions;

    protected override void Start()
    {
        base.Start();

        m_playGameButton.onClick.AddListener(PlayGame);

        m_buttons = new[]
        {
            m_playGameButton,
            m_levelSelectButton,
            m_settingsButton,
            m_quitButton
        };

        Cache();
        PrepareInitialState();
        AnimateIntro();
    }

    private void Cache()
    {
        int count = m_buttons.Length;

        m_rects = new RectTransform[count];
        m_canvasGroups = new CanvasGroup[count];
        m_finalPositions = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            m_rects[i] = m_buttons[i].GetComponent<RectTransform>();

            m_canvasGroups[i] = m_buttons[i].GetComponent<CanvasGroup>();
            if (m_canvasGroups[i] == null)
                m_canvasGroups[i] = m_buttons[i].gameObject.AddComponent<CanvasGroup>();

            m_finalPositions[i] = m_rects[i].anchoredPosition;
        }
    }

    private void PrepareInitialState()
    {
        for (int i = 0; i < m_buttons.Length; i++)
        {
            var rect = m_rects[i];
            var cg = m_canvasGroups[i];

            rect.anchoredPosition = m_finalPositions[i] + new Vector2(0, m_yOffset);

            cg.alpha = 0f;

            m_buttons[i].SetInactive(true);
        }
    }

    private void AnimateIntro()
    {
        for (int i = 0; i < m_buttons.Length; i++)
        {
            int index = i;

            var rect = m_rects[index];
            var cg = m_canvasGroups[index];

            float delay = index * m_staggerDelay;

            // Fade in
            LeanTween.alphaCanvas(cg, 1f, m_fadeDuration)
                .setDelay(delay);

            // Move down to final position
            LeanTween.move(rect, m_finalPositions[index], m_moveDuration)
                .setDelay(delay)
                .setEase(m_ease)
                .setOnComplete(() =>
                {
                    m_buttons[index].SetInactive(false);
                });
        }
    }

    private void PlayGame()
    {
        GameStateManager.Instance.SetState(
            new LoadingState(GameStateManager.Instance, m_sceneName));
    }
}