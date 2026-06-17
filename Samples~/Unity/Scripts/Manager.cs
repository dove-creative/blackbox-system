using System;
using System.IO;
using UnityEngine;

namespace BlackThunder.BlackboxSystem.Samples
{
    public class Manager : MonoBehaviour
    {
        [SerializeField] private Sample_1_Write _sample1;
        [SerializeField] private Sample_2_Exert _sample2;
        [SerializeField] private Sample_3_Tag _sample3;
        [SerializeField] private Sample_4_Exception _sample4;

        private bool _isRunning;

        private GUIStyle _buttonStyle = new GUIStyle();
        private bool _isStyleReady;

        #region UI Configurations
        private const float PanelX = 24f;
        private const float PanelY = 24f;
        private const float PanelWidth = 300f;
        private const float PanelHeight = 236f;
        private const float PanelPadding = 18f;
        private const float ButtonHeight = 38f;
        private const float ButtonSpacing = 16f;
        private const float OpenButtonTop = 288f;
        private const float OpenButtonWidth = PanelWidth;
        private const float OpenButtonHeight = 52f;
        #endregion


        private void OnGUI()
        {
            EnsureStyles();

            GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), GUIContent.none);

            var buttonWidth = PanelWidth - PanelPadding * 2f;
            var buttonX = PanelX + PanelPadding;
            var buttonY = PanelY + PanelPadding;

            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, ButtonHeight), "1: Write", _buttonStyle))
                RunSample1();

            buttonY += ButtonHeight + ButtonSpacing;
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, ButtonHeight), "2: Exert", _buttonStyle))
                RunSample2();

            buttonY += ButtonHeight + ButtonSpacing;
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, ButtonHeight), "3: Tag", _buttonStyle))
                RunSample3();

            buttonY += ButtonHeight + ButtonSpacing;
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, ButtonHeight), "4: Exception", _buttonStyle))
                RunSample4();

            if (GUI.Button(new Rect(PanelX, OpenButtonTop, OpenButtonWidth, OpenButtonHeight), "Open Log Folder", _buttonStyle))
                OpenLogFolder();
        }

        private void RunSample1()
        {
            if (_isRunning || _sample1 == null) return;
            _isRunning = true;

            BlackboxHandle.ForceReset();
            _sample1.Run();

            _isRunning = false;
        }

        private void RunSample2()
        {
            if (_isRunning || _sample2 == null) return;
            _isRunning = true;

            BlackboxHandle.ForceReset();
            _sample2.Run();

            _isRunning = false;
        }

        private void RunSample3()
        {
            if (_isRunning || _sample3 == null) return;
            _isRunning = true;

            BlackboxHandle.ForceReset();
            _sample3.Run();

            _isRunning = false;
        }

        private void RunSample4()
        {
            if (_isRunning || _sample4 == null) return;
            _isRunning = true;

            try
            {
                BlackboxHandle.ForceReset();
                _sample4.Run();
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void OpenLogFolder()
        {
            var directory = Path.Combine(Application.persistentDataPath, "BlackboxSystem", "Samples");
            Directory.CreateDirectory(directory);
            Application.OpenURL(new Uri(directory).AbsoluteUri);
        }

        private void EnsureStyles()
        {
            if (_isStyleReady)
                return;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _isStyleReady = true;
        }
    }
}
