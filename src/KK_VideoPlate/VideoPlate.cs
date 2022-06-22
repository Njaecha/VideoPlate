using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine;
using KKAPI;
using Studio;

namespace VideoPlate
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency("com.joan6694.illusionplugins.timeline")]
    [BepInDependency(KK_Plugins.MaterialEditor.MaterialEditorPlugin.PluginGUID)]
    [BepInProcess("CharaStudio")]
    public class VideoPlate : BaseUnityPlugin
    {
        public const string PluginName = "KK_VideoPlate";
        public const string GUID = "org.njaecha.plugins.VideoPlate";
        public const string Version = "1.1.1";

        internal new static ManualLogSource Logger;

        internal static Studio.TreeNodeCtrl treeNodeCtrl;

        internal static Dictionary<VideoPlatePlayer, OCIItem> playerItems = new Dictionary<VideoPlatePlayer, OCIItem>();

        // timelineObserver
        internal static bool plateWithTimelinemodeExists = false;
        private bool TOisPlayingOld = false;
        private float TOplaybackTimeOld = 0.0f;

        // ui
        private static bool ui = false;
        private static bool ui2 = false;
        private Rect windowRect = new Rect(500, 100, 200, 140);
        private Rect windowRect2 = new Rect(700, 100, 500, 200);
        private static VideoPlatePlayer lastSelectedPlayer;
        private static String lspURL = "";
        private static String lspAlphaURL = "";
        private static float lspVolumeOld = 0.2f;
        private static float lspVolume = 0.2f;
        private static float lspTime = 0;
        private static float lspTimeOld = 0;
        private static string lspItemName = "";
        private static bool lspAdvanced = false;

        private ConfigEntry<KeyboardShortcut> uiShortcut;
        private ConfigEntry<String> defaultDirectory;

        internal static int plateIndex = 0;

        //hotkeys
        private ConfigEntry<bool> hotkeyActive;

        private ConfigEntry<KeyboardShortcut> SCplayPause;
        private ConfigEntry<KeyboardShortcut> SCplayPauseGlobal;
        private ConfigEntry<KeyboardShortcut> SCstop;
        private ConfigEntry<KeyboardShortcut> SCstopGlobal;
        private ConfigEntry<KeyboardShortcut> SCnextFrame;
        private ConfigEntry<KeyboardShortcut> SCnextFrameGlobal;
        private ConfigEntry<KeyboardShortcut> SCprevFrame;
        private ConfigEntry<KeyboardShortcut> SCprevFrameGlobal;
        private ConfigEntry<KeyboardShortcut> SCvolumeUp;
        private ConfigEntry<KeyboardShortcut> SCvolumeDown;
        private ConfigEntry<KeyboardShortcut> SCtimelineMode;



        void Awake()
        {

            VideoPlate.Logger = base.Logger;
            KKAPI.Studio.StudioAPI.StudioLoadedChanged += registerCtrls;
            KKAPI.Studio.SaveLoad.StudioSaveLoadApi.RegisterExtraBehaviour<SceneController>(GUID);

            uiShortcut = Config.Bind("_General_", "Open UI Shortcut", new KeyboardShortcut(KeyCode.V, KeyCode.LeftAlt), "Keyboard Shortcut to open the UI");
            defaultDirectory = Config.Bind("_General_", "Default Directory", "C:/", "directory to open the open file dialogue in");

            hotkeyActive = Config.Bind("_General_", "Hotkeys Active", true, "Turn the keyboard shortcuts on or off");
            SCplayPause = Config.Bind("Keyboard Shorcuts", "Play/Pause", new KeyboardShortcut(KeyCode.KeypadMultiply), "Plays/Pauses the selected player");
            SCplayPauseGlobal = Config.Bind("Keyboard Shorcuts", "Play/Pause (Global)", new KeyboardShortcut(KeyCode.KeypadMultiply, KeyCode.LeftAlt), "Plays/Pauses all players");
            SCstop = Config.Bind("Keyboard Shorcuts", "Stop", new KeyboardShortcut(KeyCode.Keypad9), "Stops the selected player");
            SCstopGlobal = Config.Bind("Keyboard Shorcuts", "Stop (Global)", new KeyboardShortcut(KeyCode.Keypad9, KeyCode.LeftAlt), "Stops all players");
            SCnextFrame = Config.Bind("Keyboard Shorcuts", "Next Frame", new KeyboardShortcut(KeyCode.KeypadMinus), "Jumps forward one frame on the selected player");
            SCnextFrameGlobal = Config.Bind("Keyboard Shorcuts", "Next Frame (Global)", new KeyboardShortcut(KeyCode.KeypadMinus, KeyCode.LeftAlt), "Jumps forward one frame on all players");
            SCprevFrame = Config.Bind("Keyboard Shorcuts", "Previous Frame", new KeyboardShortcut(KeyCode.KeypadDivide), "Jumps backwards one frame on the selected player");
            SCprevFrameGlobal = Config.Bind("Keyboard Shorcuts", "Previous Frame (Global)", new KeyboardShortcut(KeyCode.KeypadDivide, KeyCode.LeftAlt), "Jumps backwards one frame on all players");
            SCvolumeUp = Config.Bind("Keyboard Shorcuts", "Volume Up", new KeyboardShortcut(KeyCode.F7), "Increases the volume on the selected player");
            SCvolumeDown = Config.Bind("Keyboard Shorcuts", "Volume Down", new KeyboardShortcut(KeyCode.F6), "Decreases the volume on the selected player");
            SCtimelineMode = Config.Bind("Keyboard Shorcuts", "Toggle Timelinemode", new KeyboardShortcut(KeyCode.F4), "Enables/Disables Timelinemode on the selected player");

        }

        private void registerCtrls(object sender, EventArgs e)
        {
            // treeNodeCtrl
            treeNodeCtrl = Singleton<Studio.Studio>.Instance.treeNodeCtrl;
            treeNodeCtrl.onSelect += setGuiContent;
        }

        internal static void setGuiContent(TreeNodeObject tno = null)
        {
            if (!ui)
                return;
            if (tno == null)
            {
                tno = treeNodeCtrl.selectNode;
                if (tno == null) return;
            }
            if (KKAPI.Studio.StudioAPI.GetSelectedObjects().Count() == 0) return;
            ObjectCtrlInfo selectedObject = KKAPI.Studio.StudioAPI.GetSelectedObjects().First();
            if (selectedObject is OCIItem)
            {
                OCIItem item = (OCIItem)selectedObject;
                VideoPlatePlayer vpp = item.objectItem.GetComponent<VideoPlatePlayer>();
                if (vpp != null && vpp.player != null)
                {
                    lastSelectedPlayer = vpp;
                    lspURL = vpp.player.url;
                    lspVolume = vpp.volume;
                    lspItemName = item.treeNodeObject.textName;
                    if (vpp.hasAlphaSubsidary)
                        lspAlphaURL = vpp.alphaPlayer.url;
                    else lspAlphaURL = "";
                }
            }
        }

        void OnGUI()
        {
            if (ui)
            {
                windowRect = GUI.Window(8753, windowRect, WindowFunction, $"{PluginName} v{Version}");
                KKAPI.Utilities.IMGUIUtils.EatInputInRect(windowRect);
                if (ui2)
                {
                    windowRect2 = GUI.Window(8754, windowRect2, WindowFunction2, lspItemName);
                    KKAPI.Utilities.IMGUIUtils.EatInputInRect(windowRect2);
                }
            }
        }

        private void WindowFunction(int WindowID)
        {
            GUIStyle textCentered = new GUIStyle();
            textCentered.alignment = TextAnchor.MiddleCenter;
            textCentered.wordWrap = true;
            textCentered.normal.textColor = Color.white;
            if (GUI.Button(new Rect(10, 20, 180, 30), "Add VideoPlate"))
            {
                addVideoPlate();
            }
            if (GUI.Button(new Rect(10, 50, 180, 30), ui2 ? "Close Controlpanel" : "Open Controlpanel"))
            {
                ui2 = !ui2;
            }
            if (lastSelectedPlayer == null) GUI.enabled = false;
            GUI.Label(new Rect(10, 80, 180, 20), "Master Controls", textCentered);
            if (GUI.Button(new Rect(10, 105, 30, 25), "▌▌"))
            {
                PauseAll();
            }
            if (GUI.Button(new Rect(40, 105, 20, 25), "<"))
            {
                PrevFrameAll();
            }
            if (GUI.Button(new Rect(windowRect.width / 2 - 40, 105, 80, 25), "▶️"))
            {
                PlayAll();
            }
            if (GUI.Button(new Rect(windowRect.width - 60, 105, 20, 25), ">"))
            {
                NextFrameAll();
            }
            if (GUI.Button(new Rect(windowRect.width - 40, 105, 30, 25), "▐█▌"))
            {
                StopAll();
            }
            GUI.enabled = true;
            GUI.DragWindow();
        }
        private void WindowFunction2(int WindowID)
        {
            windowRect2.height = 200;
            GUIStyle textCentered = new GUIStyle();
            textCentered.alignment = TextAnchor.MiddleCenter;
            textCentered.wordWrap = true;
            textCentered.normal.textColor = Color.white;
            GUIStyle textRight = new GUIStyle();
            textRight.alignment = TextAnchor.MiddleRight;
            textRight.wordWrap = true;
            textRight.normal.textColor = Color.white;
            if (lastSelectedPlayer != null)
            {
                VideoPlatePlayer vpp = lastSelectedPlayer;
                if (vpp.player == null) return;
                if (!vpp.player.canSetTime || vpp.player.isPlaying || vpp.isTimelineMode)
                    GUI.enabled = false;
                if (vpp.player.isPlaying || vpp.isTimelineMode)
                    lspTime = (float)vpp.player.time;
                lspTime = GUI.HorizontalSlider(new Rect(10, 20, 480, 20), lspTime, 0.0f, (float)vpp.player.frameCount / vpp.player.frameRate);
                if (lspTime != lspTimeOld && !lastSelectedPlayer.player.isPlaying && !vpp.isTimelineMode)
                {
                    vpp.trySetTime(lspTime);
                    lspTimeOld = lspTime;
                }
                GUI.enabled = true;
                GUI.Label(new Rect(10, 30, 150, 20), $"{TimeSpan.FromSeconds(vpp.player.time)}");
                if (vpp.player.frameCount != 0 || vpp.player.frameRate != 0)
                    GUI.Label(new Rect(windowRect2.width - 130, 30, 120, 20), $"{TimeSpan.FromSeconds(vpp.player.frameCount / vpp.player.frameRate)}", textRight);
                else
                    GUI.Label(new Rect(windowRect2.width - 130, 30, 120, 20), $"{TimeSpan.FromSeconds(0)}", textRight);
                if (vpp.isTimelineMode)
                    GUI.enabled = false;
                if (GUI.Button(new Rect(10, 50, 100, 40), vpp.player.isLooping ? "☑️ Looping" : "☐ Looping"))
                {
                    vpp.setLoop(!vpp.player.isLooping);
                }

                if (GUI.Button(new Rect(110, 50, 50, 40), "<"))
                {
                    vpp.prevFrame();
                }

                if (GUI.Button(new Rect(windowRect2.width / 2 - 90, 50, 180, 40), vpp.player.isPlaying ? "Pause" : "Play"))
                {
                    lspTime = lspTimeOld = (float)vpp.player.time;
                    vpp.PlayPause();
                }

                if (GUI.Button(new Rect(windowRect2.width / 2 + 90, 50, 50, 40), ">"))
                {
                    vpp.nextFrame();
                }

                if (GUI.Button(new Rect(windowRect2.width - 110, 50, 100, 40), "Stop"))
                {
                    vpp.Stop();
                    lspTime = lspTimeOld = 0;
                }
                GUI.enabled = true;
                if (!vpp.audioSource.enabled)
                    GUI.enabled = false;
                GUI.Label(new Rect(10, 100, 50, 20), "Volume:");
                lspVolume = GUI.HorizontalSlider(new Rect(65, 106, 280, 30), lspVolume, 0.0f, 1.0f);
                GUI.enabled = true;
                if (GUI.Button(new Rect(10, 130, 60, 30), "Load"))
                {
                    vpp.setVideo(lspURL);
                }
                lspURL = GUI.TextField(new Rect(70, 130, 390, 30), lspURL);
                if (GUI.Button(new Rect(windowRect2.width - 40, 130, 30, 30), "..."))
                {
                    KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
                        KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST |
                        KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
                        KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
                    string[] file = KKAPI.Utilities.OpenFileDialog.ShowDialog("Select Video", defaultDirectory.Value, "Video files (*.mp4; *.avi; *.mov; *.m4v)|*.mp4; *.avi; *.mov; *.m4v | All files (*.*)|*.*", "mp4", SingleFileFlags);
                    if (file != null)
                    {
                        lspURL = file[0];
                    }
                }

                if (vpp.player.texture != null)
                    GUI.Label(new Rect(10, 165, 480, 30), $"Resolution: {vpp.player.texture.width}x{vpp.player.texture.height} | Framerate: {vpp.player.frameRate}fps", textCentered);
                else
                    GUI.Label(new Rect(10, 165, 480, 30), $"Resolution: 0x0 | Framerate: 0fps", textCentered);

                if (GUI.Button(new Rect(windowRect2.width - 150, 95, 140, 30), vpp.isTimelineMode ? "☑️ Timelinemode" : "☐ Timelinemode"))
                {
                    toggleTimelineMode(vpp);
                }
                if (vpp.isTimelineMode)
                {
                    windowRect2.height += 30;

                    GUI.Label(new Rect(10, 200, 300, 20), $"Start Offset: {TimeSpan.FromSeconds(vpp.timelineStartOffset).Minutes}:{TimeSpan.FromSeconds(vpp.timelineStartOffset).Seconds} (Timeline)");
                    if (GUI.Button(new Rect(windowRect2.width - 150, 200, 140, 20), "Refer to current"))
                    {
                        vpp.timelineStartOffset = Timeline.Timeline.playbackTime;
                        vpp.trySetTime(Timeline.Timeline.playbackTime - vpp.timelineStartOffset);
                    }
                }
                if (GUI.Button(new Rect(windowRect2.width - 100, 165, 90, 30), lspAdvanced ? "Close" : "Advanced"))
                    lspAdvanced = !lspAdvanced;
                if (lspAdvanced)
                {
                    float advTop = windowRect2.height;
                    windowRect2.height += 130;
                    if (lastSelectedPlayer.url == "")
                        GUI.enabled = false;
                    if (GUI.Button(new Rect(windowRect2.width / 2 - 80, advTop, 160, 30), "Mirror Vertical"))
                    {
                        lastSelectedPlayer.mirrorX();
                    }
                    if (GUI.Button(new Rect(windowRect2.width - 170, advTop, 160, 30), "Mirror Horizontal"))
                    {
                        lastSelectedPlayer.mirrorY();
                    }
                    if (GUI.Button(new Rect(10, advTop, 160, 30), lastSelectedPlayer.isDoubleSided ? "☑️ Double Sided" : "☐ Double Sided"))
                    {
                        lastSelectedPlayer.setDoubleSided(!lastSelectedPlayer.isDoubleSided);
                    }
                    if (GUI.Button(new Rect(windowRect2.width - 170, advTop + 35, 160, 30), lastSelectedPlayer.isAudio3D ? "☐ Global Audio" : "☑️ Global Audio"))
                    {
                        lastSelectedPlayer.setAudio3D(!lastSelectedPlayer.isAudio3D);
                    }
                    if (GUI.Button(new Rect(10, advTop + 35, 160, 30), lastSelectedPlayer.alphaEnabled ? "☑️ AlphaMask" : "☐ AlphaMask"))
                    {
                        lastSelectedPlayer.toggleAlphaMask();
                    }
                    if (!lastSelectedPlayer.hasAlphaSubsidary)
                        GUI.enabled = false;
                    GUI.Label(new Rect(10, advTop + 70, 480, 20), "Video for AlphaMask (enter nothing to use main video)", textCentered);
                    if (GUI.Button(new Rect(10, advTop + 90, 60, 30), "Load"))
                    {
                        vpp.setAlphaVideo(lspAlphaURL);
                    }
                    lspAlphaURL = GUI.TextField(new Rect(70, advTop + 90, 390, 30), lspAlphaURL);
                    if (GUI.Button(new Rect(windowRect2.width - 40, advTop + 90, 30, 30), "..."))
                    {
                        KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags =
                            KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST |
                            KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES |
                            KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
                        string[] file = KKAPI.Utilities.OpenFileDialog.ShowDialog("Select Video", defaultDirectory.Value, "Video files (*.mp4; *.avi; *.mov; *.m4v)|*.mp4; *.avi; *.mov; *.m4v | All files (*.*)|*.*", "mp4", SingleFileFlags);
                        if (file != null)
                        {
                            lspAlphaURL = file[0];
                        }
                    }
                    GUI.enabled = true;
                }

            }
            else
            {
                GUI.Label(new Rect(0, 0, windowRect2.width, windowRect2.height), "Please select a VideoPlate!", textCentered);
            }
            GUI.DragWindow();
        }

        private void toggleTimelineMode(VideoPlatePlayer vpp)
        {
            if (vpp.isTimelineMode)
            {
                vpp.isTimelineMode = false;
                bool x = false;
                foreach (VideoPlatePlayer vpp_ in playerItems.Keys)
                {
                    if (vpp_.isTimelineMode)
                    {
                        x = true;
                    }
                }
                if (!x)
                {
                    plateWithTimelinemodeExists = false;
                }
            }
            else
            {
                vpp.player.isLooping = false;
                vpp.isTimelineMode = true;
                plateWithTimelinemodeExists = true;
            }
        }

        public static VideoPlatePlayer addVideoPlate(OCIItem plateItem = null)
        {
            if (plateItem == null)
            {
                plateItem = Studio.AddObjectItem.Add(1, 1, 1);
            }
            plateItem.treeNodeObject.textName = $"VideoPlate {plateIndex}";

            plateIndex++;

            VideoPlatePlayer vpp = plateItem.objectItem.GetOrAddComponent<VideoPlatePlayer>();
            vpp.Init(plateItem.objectItem);

            playerItems[vpp] = plateItem;

            if (lastSelectedPlayer == null)
                lastSelectedPlayer = vpp;

            setGuiContent();

            return vpp;
        }

        public void PlayAll(bool timelineOnly = false)
        {
            if (lastSelectedPlayer == null) return;
            foreach (VideoPlatePlayer vpp in playerItems.Keys)
            {
                if (timelineOnly)
                {
                    if (vpp.isTimelineMode)
                    {
                        bool synced = vpp.trySetTime(Timeline.Timeline.playbackTime - vpp.timelineStartOffset);
                        if (synced)
                        {
                            if (!vpp.player.isPlaying)
                                vpp.PlayPause();
                        }
                    }
                }
                else if (!vpp.player.isPlaying)
                    vpp.PlayPause();
            }
        }
        public void PauseAll(bool timelineOnly = false)
        {
            if (lastSelectedPlayer == null) return;
            lspTime = lspTimeOld = (float)lastSelectedPlayer.player.time;
            foreach (VideoPlatePlayer vpp in playerItems.Keys)
            {
                if (timelineOnly)
                {
                    if (vpp.isTimelineMode)
                    {
                        if (vpp.player.isPlaying)
                            vpp.PlayPause();
                    }
                }
                else if (vpp.player.isPlaying)
                    vpp.PlayPause();
            }
        }

        public void StopAll(bool timelineOnly = false)
        {
            foreach (VideoPlatePlayer vpp in playerItems.Keys)
            {
                if (timelineOnly)
                {
                    if (vpp.isTimelineMode)
                        vpp.Stop();
                }
                else vpp.Stop();
            }
            if (lastSelectedPlayer == null) return;
            lspTime = lspTimeOld = 0;
        }

        public void SetTimeAll(double time, bool timelineOnly = false)
        {
            foreach (VideoPlatePlayer vpp in playerItems.Keys)
            {
                if (timelineOnly)
                {
                    if (vpp.isTimelineMode)
                        vpp.trySetTime(time - vpp.timelineStartOffset);
                }
                else vpp.trySetTime(time);
            }
        }

        public void NextFrameAll(bool timelineOnly = false)
        {
            foreach (VideoPlatePlayer vpp in playerItems.Keys)
            {
                if (timelineOnly)
                {
                    if (vpp.isTimelineMode)
                        vpp.nextFrame();
                }
                else vpp.nextFrame();
            }
        }
        public void PrevFrameAll(bool timelineOnly = false)
        {
            foreach (VideoPlatePlayer vpp in playerItems.Keys)
            {
                if (timelineOnly)
                {
                    if (vpp.isTimelineMode)
                        vpp.prevFrame();
                }
                else vpp.prevFrame();
            }
        }

        void Update()
        {
            if (uiShortcut.Value.IsDown())
            {
                ui = !ui;
            }

            if (lspVolume != lspVolumeOld && lastSelectedPlayer != null)
            {
                lspVolumeOld = lspVolume;
                lastSelectedPlayer.setVolume(lspVolume);
            }

            //hotkeys
            if (hotkeyActive.Value && lastSelectedPlayer != null)
            {
                if (SCplayPause.Value.IsDown())
                {
                    lspTime = lspTimeOld = (float)lastSelectedPlayer.player.time;
                    lastSelectedPlayer.PlayPause();
                }
                if (SCplayPauseGlobal.Value.IsDown())
                {
                    if (lastSelectedPlayer.player.isPlaying)
                        PauseAll();
                    else PlayAll();
                }
                if (SCstop.Value.IsDown())
                {
                    lastSelectedPlayer.Stop();
                    lspTime = lspTimeOld = 0;
                }
                if (SCstopGlobal.Value.IsDown())
                {
                    StopAll();
                }
                if (SCnextFrame.Value.IsDown())
                {
                    lastSelectedPlayer.nextFrame();
                }
                if (SCnextFrameGlobal.Value.IsDown())
                {
                    NextFrameAll();
                }
                if (SCprevFrame.Value.IsDown())
                {
                    lastSelectedPlayer.prevFrame();
                }
                if (SCprevFrameGlobal.Value.IsDown())
                {
                    PrevFrameAll();
                }
                if (SCvolumeUp.Value.IsDown())
                {
                    if (lastSelectedPlayer.volume <= 0.9)
                    {
                        lspVolume = lastSelectedPlayer.volume + 0.1f;
                        lastSelectedPlayer.setVolume(lastSelectedPlayer.volume + 0.1f);
                    }
                    else if (lastSelectedPlayer.volume > 0.9 && lastSelectedPlayer.volume < 1f)
                    {
                        lspVolume = 1f;
                        lastSelectedPlayer.setVolume(1f);
                    }
                }
                if (SCvolumeDown.Value.IsDown())
                {
                    if (lastSelectedPlayer.volume >= 0.1)
                    {
                        lspVolume = lastSelectedPlayer.volume - 0.1f;
                        lastSelectedPlayer.setVolume(lastSelectedPlayer.volume - 0.1f);
                    }
                    else if (lastSelectedPlayer.volume < 0.1 && lastSelectedPlayer.volume > 0)
                    {
                        lspVolume = 0;
                        lastSelectedPlayer.setVolume(0);
                    }
                }
                if (SCtimelineMode.Value.IsDown())
                {
                    toggleTimelineMode(lastSelectedPlayer);
                }

            }

            //timeline observer
            if (plateWithTimelinemodeExists)
            {
                if (Timeline.Timeline.isPlaying != TOisPlayingOld) //onChangePlaying
                {
                    TOisPlayingOld = Timeline.Timeline.isPlaying;
                    if (Timeline.Timeline.isPlaying)
                        PlayAll(true);
                    else
                        PauseAll(true);
                }
                if (Timeline.Timeline.playbackTime != TOplaybackTimeOld) //onChangePlaybackTime
                {
                    if (!Timeline.Timeline.isPlaying)
                    {
                        SetTimeAll(Timeline.Timeline.playbackTime, true);
                    }
                    else if (TOplaybackTimeOld - Timeline.Timeline.playbackTime < -0.1f || TOplaybackTimeOld - Timeline.Timeline.playbackTime > 0.1f)
                        SetTimeAll(Timeline.Timeline.playbackTime, true);
                    if (Timeline.Timeline.playbackTime >= Timeline.Timeline.duration - 0.02) //onTimelineEndReached
                    {
                        StopAll(true);
                    }
                    TOplaybackTimeOld = Timeline.Timeline.playbackTime;
                }
                if (Timeline.Timeline.isPlaying)  // plays the players if they didn't play onChangePlaying because they have to start late (timelineStartTime)
                {
                    foreach (VideoPlatePlayer vpp in playerItems.Keys)
                    {
                        if (vpp.isTimelineMode &&
                            !vpp.player.isPlaying &&
                            Timeline.Timeline.playbackTime >= vpp.timelineStartOffset &&
                            Timeline.Timeline.playbackTime < vpp.timelineStartOffset + 0.05)
                        {
                            vpp.trySetTime(Timeline.Timeline.playbackTime - vpp.timelineStartOffset);
                            vpp.player.Play();
                        }
                    }
                }

            }

        }

    }
}
