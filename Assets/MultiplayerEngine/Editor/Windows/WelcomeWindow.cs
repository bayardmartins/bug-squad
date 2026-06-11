using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Welcome window shown at Unity startup for Multiplayer Engine.
    /// Standardized to inherit from MEEditorWindow and styled via MEEditorTheme.
    /// </summary>
    [InitializeOnLoad]
    public class WelcomeWindow : MEEditorWindow
    {
        private static string steamPackage = "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#20.0.0";

        private static string[] unityServices = new string[]
        {
            "com.unity.services.multiplayer",
            "com.unity.services.friends",
            "com.unity.services.authentication",
            "com.unity.services.cloudsave",
            "com.unity.services.vivox"
        };

        private static int currentPage = 0;
        private bool dontShowAgain;

        protected override bool UseGlobalScrollView => true;
        protected override string WindowSubtitle => currentPage == 0 ? "Introduction & Quickstart" : "Choose Multiplayer Backend";

        static WelcomeWindow()
        {
            EditorApplication.update += ShowOnStartup;
        }

        private static void ShowOnStartup()
        {
            EditorApplication.update -= ShowOnStartup;
            if (EditorPrefs.GetBool("WelcomeWindow_DontShow", false))
                return;
            ShowWindow();
        }

        [MenuItem("Tools/Multiplayer Engine/Welcome Window", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<WelcomeWindow>(true, "Welcome!!", true);
            window.minSize = new Vector2(520, 780);
        }

        [MenuItem("Tools/Multiplayer Engine/Switch Backend", false, 1)]
        public static void ShowBackendPage()
        {
            var window = GetWindow<WelcomeWindow>(true, "Change Backend", true);
            window.minSize = new Vector2(520, 780);
            currentPage = 1;
        }

        private void OnEnable()
        {
            dontShowAgain = EditorPrefs.GetBool("WelcomeWindow_DontShow", false);
            titleContent = new GUIContent("Welcome!!", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image);
        }

        protected override void DrawHeaderBanner()
        {
            // Draw page-specific cover banner
            string bannerPath = currentPage == 0 
                ? "Assets/MultiplayerEngine/Editor/Images/Background.png" 
                : "Assets/MultiplayerEngine/Editor/Images/Backend.png";

            Texture2D coverImage = (Texture2D)EditorGUIUtility.Load(bannerPath);
            MEEditorTheme.DrawHeader("Multiplayer Engine", WindowSubtitle, coverImage);
        }

        protected override void DrawBody()
        {
            if (currentPage == 0)
                DrawPageOne();
            else
                DrawPageTwo();

            DrawFooterNavigation();
        }

        #region PAGE ONE
        private void DrawPageOne()
        {
            MEEditorTheme.BeginCard("Welcome to Multiplayer Engine Pro!");

            GUILayout.Label(
                "Thank you for choosing the Multiplayer Engine!\n\n" +
                "This production-ready framework includes everything you need to kickstart a high-quality co-op or multiplayer project. " +
                "It features secure authentication, lobby matchmaking, robust friend lists, premium voice chat with dynamic 3D proximity support, " +
                "and an advanced third-person controller with multiplayer-synced melee and shooter mechanics.\n\n" +
                "Accelerate your workflow with pre-built modular scripts, allowing you to focus purely on designing your unique gameplay experience.\n\n" +
                "Need help or want to stay updated? Tap below to join our developer community!",
                MEEditorTheme.StyleDescription, GUILayout.ExpandWidth(true));

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Visit Website", MEEditorTheme.StyleSecondaryButton, GUILayout.ExpandWidth(true)))
            {
                Application.OpenURL("https://ignitivelabs.net");
            }
            if (GUILayout.Button("Join Discord", MEEditorTheme.StylePrimaryButton, GUILayout.ExpandWidth(true)))
            {
                Application.OpenURL("https://discord.gg/59cFVYavpd");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);
            MEEditorTheme.DrawDivider();
            GUILayout.Space(15);

            // Sleek transition button
            if (GUILayout.Button("Let's set up your backend service next →", MEEditorTheme.StylePrimaryButton, GUILayout.ExpandWidth(true)))
            {
                currentPage = 1;
            }

            MEEditorTheme.EndCard();
        }
        #endregion

        #region PAGE TWO (BACKEND SWITCHER)
        private void DrawPageTwo()
        {
            MEEditorTheme.BeginCard("Backend Service Switcher");

            GUILayout.Label(
                "Configure your project's multiplayer network infrastructure. " +
                "Easily switch between Steamworks (Steam P2P) or Unity Services (Relay/Lobby). " +
                "Only one active backend define can be compiled at a time.",
                MEEditorTheme.StyleDescription, GUILayout.ExpandWidth(true));

            GUILayout.Space(20);

            // Switch buttons
            bool isSteam = IsDefineSet("STEAM_SERVICES");
            bool isUnity = IsDefineSet("UNITY_SERVICES");

            GUIStyle switchButtonStyle = new GUIStyle(MEEditorTheme.StyleSecondaryButton)
            {
                fixedHeight = 44
            };

            GUIStyle activeSwitchButtonStyle = new GUIStyle(MEEditorTheme.StylePrimaryButton)
            {
                fixedHeight = 44
            };

            GUILayout.BeginHorizontal();
            if (isSteam)
            {
                GUILayout.Button("Steam (Active)", activeSwitchButtonStyle, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Switch to Unity Services", switchButtonStyle, GUILayout.ExpandWidth(true)))
                    InstallUnityServices();
            }
            else if (isUnity)
            {
                if (GUILayout.Button("Switch to Steam", switchButtonStyle, GUILayout.ExpandWidth(true)))
                    InstallSteam();
                GUILayout.Button("Unity Services (Active)", activeSwitchButtonStyle, GUILayout.ExpandWidth(true));
            }
            else
            {
                if (GUILayout.Button("Activate Steam", switchButtonStyle, GUILayout.ExpandWidth(true)))
                    InstallSteam();
                if (GUILayout.Button("Activate Unity Services", switchButtonStyle, GUILayout.ExpandWidth(true)))
                    InstallUnityServices();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            // Status display box
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Texture2D greenCircle = EditorGUIUtility.IconContent("TestPassed").image as Texture2D;
            Texture2D defaultCircle = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
            
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            statusStyle.normal.textColor = MEEditorTheme.ColorTextNormal;

            if (isSteam)
            {
                GUILayout.Label(new GUIContent("  Steam Backend Status: Active & Operational", greenCircle), statusStyle, GUILayout.Height(32));
            }
            else if (isUnity)
            {
                GUILayout.Label(new GUIContent("  Unity Services Status: Active & Operational", greenCircle), statusStyle, GUILayout.Height(32));
            }
            else
            {
                statusStyle.normal.textColor = MEEditorTheme.ColorWarning;
                GUILayout.Label(new GUIContent("  No Backend Services Configured", defaultCircle), statusStyle, GUILayout.Height(32));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);
            MEEditorTheme.DrawDivider();
            GUILayout.Space(15);

            // Document buttons in card
            GUILayout.Label("Service Dashboards & Developer APIs", EditorStyles.boldLabel);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Steam Docs", MEEditorTheme.StyleSecondaryButton, GUILayout.ExpandWidth(true)))
                Application.OpenURL("https://steamworks.github.io/");
            if (GUILayout.Button("Unity Docs", MEEditorTheme.StyleSecondaryButton, GUILayout.ExpandWidth(true)))
                Application.OpenURL("https://docs.unity.com/");
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Steam Partner Dashboard", MEEditorTheme.StyleSecondaryButton, GUILayout.ExpandWidth(true)))
                Application.OpenURL("https://partner.steamgames.com/");
            if (GUILayout.Button("Unity Dashboard", MEEditorTheme.StyleSecondaryButton, GUILayout.ExpandWidth(true)))
                Application.OpenURL("https://dashboard.unity3d.com/");
            GUILayout.EndHorizontal();

            MEEditorTheme.EndCard();

            GUILayout.Space(10);

            // Don't Show Again toggle
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);
            bool newDontShowAgain = EditorGUILayout.Toggle("Don't Show Again on Startup", dontShowAgain);
            if (newDontShowAgain != dontShowAgain)
            {
                dontShowAgain = newDontShowAgain;
                EditorPrefs.SetBool("WelcomeWindow_DontShow", dontShowAgain);
            }
            GUILayout.EndHorizontal();
        }
        #endregion

        #region FOOTER NAVIGATION
        private void DrawFooterNavigation()
        {
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);

            if (currentPage == 1)
            {
                if (GUILayout.Button("← Back", MEEditorTheme.StyleSecondaryButton, GUILayout.Width(110)))
                    currentPage = 0;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Finish", MEEditorTheme.StylePrimaryButton, GUILayout.Width(110)))
                    Close();
            }
            else
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Next →", MEEditorTheme.StylePrimaryButton, GUILayout.Width(110)))
                    currentPage = 1;
            }

            GUILayout.Space(15);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
        #endregion

        #region HELPERS

        private static void InstallSteam()
        {
            foreach (var pkg in unityServices)
                Client.Remove(pkg);

            Client.Add(steamPackage);
            SetScriptingDefines("STEAM_SERVICES", "UNITY_SERVICES");
            Debug.Log("Steamworks.NET installation queued. UNITY_SERVICES define removed, STEAM_SERVICES added.");
        }

        private static void InstallUnityServices()
        {
            Client.Remove("com.rlabrecque.steamworks.net");
            foreach (var pkg in unityServices)
                Client.Add(pkg);

            SetScriptingDefines("UNITY_SERVICES", "STEAM_SERVICES");
            Debug.Log("Unity Services installation queued. STEAM_SERVICES define removed, UNITY_SERVICES added.");
        }

        private static void SetScriptingDefines(string addDefine, string removeDefine)
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

            List<string> defineList = new List<string>(defines.Split(';'));
            defineList.RemoveAll(d => string.IsNullOrWhiteSpace(d));
            defineList.RemoveAll(d => d == removeDefine);
            if (!defineList.Contains(addDefine))
                defineList.Add(addDefine);

            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", defineList));
        }

        private static bool IsDefineSet(string define)
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var defineList = new List<string>(defines.Split(';'));
            return defineList.Contains(define);
        }

        #endregion
    }
}
