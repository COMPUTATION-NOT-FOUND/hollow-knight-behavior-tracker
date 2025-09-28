using System;
using System.Collections.Generic;
using System.IO;
using Modding;
using UnityEngine;

namespace MyFirstMod
{
    public class MyFirstMod : Mod
    {
        // Configurable save directory
        public string saveDirectory = @"D:\HallowKnightAIData";

        private bool isRecording = false;
        private float recordingTimer = 0f;
        private float csvRecordingInterval = 1f / 3f; // 3 FPS
        private List<string> dataBuffer = new List<string>();
        private string csvFilePath;
        private string framesDirectoryPath;
        private int frameCount = 0;
        private string sessionTimestamp;

        // Performance settings
        private int targetWidth = 640;
        private int targetHeight = 360;

        public MyFirstMod() : base("Behavioral Data Collector") { }

        public override string GetVersion() => "v1.0";

        public override void Initialize()
        {
            ModHooks.HeroUpdateHook += OnHeroUpdate;

            try
            {
                // Create save directory if it doesn't exist
                if (!Directory.Exists(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                    Log($"Created main save directory: {saveDirectory}");
                }

                // Set up file paths with unique timestamps
                sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                csvFilePath = Path.Combine(saveDirectory, $"hk_actions_{sessionTimestamp}.csv");
                framesDirectoryPath = Path.Combine(saveDirectory, $"frames_{sessionTimestamp}");

                // Create frames directory
                if (!Directory.Exists(framesDirectoryPath))
                {
                    Directory.CreateDirectory(framesDirectoryPath);
                    Log($"Created frames directory: {framesDirectoryPath}");
                }

                // Write CSV header
                WriteCSVHeader();

                Log("Behavioral Data Collector initialized successfully!");
            }
            catch (Exception ex)
            {
                Log($"Error initializing mod: {ex.Message}");
            }
        }

        private void WriteCSVHeader()
        {
            string header = "frame_id,x_position,y_position,health," +
                          "moving_left,moving_right,moving_up,moving_down," +
                          "attacking,jumping,dashing,focusing";
            File.WriteAllText(csvFilePath, header + "\n");
        }

        public void OnHeroUpdate()
        {
            // Toggle recording with O key
            if (Input.GetKeyDown(KeyCode.O))
            {
                isRecording = !isRecording;
                Log($"Recording {(isRecording ? "started" : "stopped")}");

                if (!isRecording && dataBuffer.Count > 0)
                {
                    SaveBufferedData();
                    Log($"Session saved! Frames: {framesDirectoryPath}");
                }
            }

            // Record data when recording is active
            if (isRecording)
            {
                recordingTimer += Time.deltaTime;
                if (recordingTimer >= csvRecordingInterval)
                {
                    CapturePlayerData();
                    CaptureScreenFrame();
                    recordingTimer = 0f;
                }
            }
        }

        private void CapturePlayerData()
        {
            try
            {
                // Get player controller
                var heroController = HeroController.instance;
                if (heroController == null) return;

                // Get player position
                Vector3 playerPos = heroController.transform.position;
                float xPos = playerPos.x;
                float yPos = playerPos.y;

                // Get player health
                int health = PlayerData.instance.health;

                // Controller input detection
                // Movement (left stick + d-pad)
                bool movingLeft = Input.GetAxis("Horizontal") < -0.3f || Input.GetKey(KeyCode.Joystick1Button14);
                bool movingRight = Input.GetAxis("Horizontal") > 0.3f || Input.GetKey(KeyCode.Joystick1Button15);
                bool movingUp = Input.GetAxis("Vertical") > 0.3f || Input.GetKey(KeyCode.Joystick1Button12);
                bool movingDown = Input.GetAxis("Vertical") < -0.3f || Input.GetKey(KeyCode.Joystick1Button13);

                // Combat inputs
                bool attacking = Input.GetKey(KeyCode.Joystick1Button0);
                bool jumping = Input.GetKey(KeyCode.Joystick1Button1);
                bool focusing = Input.GetKey(KeyCode.Joystick1Button2);
                bool dreamnail = Input.GetKey(KeyCode.Joystick1Button3);
                //bool dashing = fix later

                // Create CSV row
                string dataRow = $"{frameCount},{xPos:F3},{yPos:F3},{health}," +
                               $"{movingLeft},{movingRight},{movingUp},{movingDown}," +
                               $"{attacking},{jumping},{dashing},{focusing}";

                // Add to buffer
                dataBuffer.Add(dataRow);
                frameCount++;

                // Save buffer periodically to avoid memory issues
                if (dataBuffer.Count >= 100)
                {
                    SaveBufferedData();
                }
            }
            catch (Exception ex)
            {
                Log($"Error capturing data: {ex.Message}");
            }
        }

        private void CaptureScreenFrame()
        {
            try
            {
                // Ensure frames directory exists
                if (!Directory.Exists(framesDirectoryPath))
                {
                    Directory.CreateDirectory(framesDirectoryPath);
                    Log($"Created frames directory: {framesDirectoryPath}");
                }

                // Capture the current screen
                Texture2D screenshot = CaptureScreen();
                if (screenshot != null)
                {
                    // Save directly to the session's frames directory
                    byte[] frameData = screenshot.EncodeToPNG();
                    string framePath = Path.Combine(framesDirectoryPath, $"frame_{frameCount:D6}.png");

                    // Ensure the directory still exists before writing file
                    string frameDir = Path.GetDirectoryName(framePath);
                    if (!Directory.Exists(frameDir))
                    {
                        Directory.CreateDirectory(frameDir);
                    }

                    File.WriteAllBytes(framePath, frameData);

                    // Clean up texture
                    UnityEngine.Object.Destroy(screenshot);
                }
            }
            catch (Exception ex)
            {
                Log($"Error capturing frame: {ex.Message}");
                Log($"Attempted path: {framesDirectoryPath}");

                // Try to recreate directory if it failed
                try
                {
                    Directory.CreateDirectory(framesDirectoryPath);
                    Log("Recreated frames directory");
                }
                catch (Exception dirEx)
                {
                    Log($"Failed to create directory: {dirEx.Message}");
                }
            }
        }

        private Texture2D CaptureScreen()
        {
            // Get current screen dimensions
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            // Calculate scale to fit target resolution while maintaining aspect ratio
            float scale = Mathf.Min((float)targetWidth / screenWidth, (float)targetHeight / screenHeight);
            int scaledWidth = Mathf.RoundToInt(screenWidth * scale);
            int scaledHeight = Mathf.RoundToInt(screenHeight * scale);

            // Capture full screen
            Texture2D fullScreenshot = new Texture2D(screenWidth, screenHeight, TextureFormat.RGB24, false);
            fullScreenshot.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
            fullScreenshot.Apply();

            // Create scaled down version (keeping RGB for Python preprocessing)
            RenderTexture rt = RenderTexture.GetTemporary(scaledWidth, scaledHeight);
            Graphics.Blit(fullScreenshot, rt);

            Texture2D scaledScreenshot = new Texture2D(scaledWidth, scaledHeight, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            scaledScreenshot.ReadPixels(new Rect(0, 0, scaledWidth, scaledHeight), 0, 0);
            scaledScreenshot.Apply();
            RenderTexture.active = null;

            // Cleanup
            UnityEngine.Object.Destroy(fullScreenshot);
            RenderTexture.ReleaseTemporary(rt);

            return scaledScreenshot;
        }

        private void SaveBufferedData()
        {
            try
            {
                if (dataBuffer.Count > 0)
                {
                    // Ensure the CSV file's directory exists
                    string csvDir = Path.GetDirectoryName(csvFilePath);
                    if (!Directory.Exists(csvDir))
                    {
                        Directory.CreateDirectory(csvDir);
                        Log($"Created CSV directory: {csvDir}");
                    }

                    File.AppendAllLines(csvFilePath, dataBuffer);
                    dataBuffer.Clear();
                }
            }
            catch (Exception ex)
            {
                Log($"Error saving CSV data: {ex.Message}");
                Log($"Attempted path: {csvFilePath}");
            }
        }
    }
}