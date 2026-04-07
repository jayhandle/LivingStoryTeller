using RimWorld;
using UnityEngine;
using Verse;

namespace LivingStoryteller
{
    public class StorytellerWindow : Window
    {
        private readonly string storytellerName;
        private readonly string narrationText;
        private readonly Texture2D portrait;
        private readonly float openedTime;

        private const float WindowWidth = 500f;
        private const float PortraitSize = 64f;
        private const float Padding = 10f;
        private const float NameHeight = 30f;
        private const float BottomPadding = 24f;
        private const float MaxWindowHeight = 400f;

        private float calculatedHeight = 180f;

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(
                    WindowWidth, calculatedHeight);
            }
        }

        public StorytellerWindow(
            string name, string text, Texture2D portrait)
        {
            this.storytellerName = name;
            this.narrationText = "\"" + text + "\"";
            this.portrait = portrait;
            this.openedTime = Time.time;

            doCloseButton = false;
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            soundAppear = SoundDefOf.LetterArrive;

            // Calculate height based on text
            float textX = portrait != null
                ? PortraitSize + Padding : 0f;
            float textWidth = WindowWidth - textX - 36f;

            Text.Font = GameFont.Small;
            float textHeight = Text.CalcHeight(
                this.narrationText, textWidth);

            // 36f = Window.Margin top + bottom (18 + 18)
            // 10f = safety buffer
            float needed = 36f + NameHeight + 4f +
                textHeight + BottomPadding + 10f;

            // At least tall enough for the portrait
            float minHeight = 36f + PortraitSize +
                BottomPadding;
            if (needed < minHeight) needed = minHeight;

            // Cap it
            if (needed > MaxWindowHeight)
                needed = MaxWindowHeight;

            calculatedHeight = needed;
        }

        protected override void SetInitialSizeAndPosition()
        {
            float x = (UI.screenWidth - WindowWidth) / 2f;
            float y = UI.screenHeight -
                calculatedHeight - 80f;
            windowRect = new Rect(
                x, y, WindowWidth, calculatedHeight);
        }

        public override void DoWindowContents(Rect inRect)
        {
            float duration =
                ModOptions.Settings.displayDuration;
            float elapsed = Time.time - openedTime;
            if (elapsed > duration)
            {
                Close(false);
                return;
            }

            // Portrait
            if (portrait != null)
            {
                Rect portraitRect = new Rect(
                    0f, 0f, PortraitSize, PortraitSize);
                GUI.DrawTexture(portraitRect, portrait,
                    ScaleMode.ScaleToFit);
            }

            // Text area
            float textX = portrait != null
                ? PortraitSize + Padding : 0f;
            float textWidth = inRect.width - textX;

            // Storyteller name
            Text.Font = GameFont.Medium;
            Rect nameRect = new Rect(
                textX, 0f, textWidth, NameHeight);
            Widgets.Label(nameRect, storytellerName);

            // Narration — use full remaining height
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.9f, 0.9f, 0.85f);
            float textY = NameHeight + 4f;
            float textHeight = inRect.height - textY -
                BottomPadding;
            Rect textRect = new Rect(
                textX, textY, textWidth, textHeight);
            Widgets.Label(textRect, narrationText);
            GUI.color = Color.white;

            // Timer bar
            float remaining = 1f - (elapsed / duration);
            Rect timerRect = new Rect(
                0f, inRect.height - 4f,
                inRect.width * remaining, 3f);
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            GUI.DrawTexture(timerRect, BaseContent.WhiteTex);
            GUI.color = Color.white;

            Text.Font = GameFont.Small;
        }
    }
}
