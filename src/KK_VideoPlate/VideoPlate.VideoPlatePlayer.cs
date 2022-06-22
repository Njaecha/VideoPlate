using UnityEngine;
using UnityEngine.Video;
using MessagePack;
using System;
using MaterialEditorAPI;

namespace VideoPlate
{
    [MessagePackObject]
    public class VideoPlatePlayerData
    {
        [Key(0)]
        public string url { get; set; }
        [Key(1)]
        public float volume { get; set; }
        [Key(2)]
        public bool isTimelineMode { get; set; }
        [Key(3)]
        public float timelineStartOffset { get; set; }
        [Key(4)]
        public bool isLooping { get; set; }
        [Key(5)]
        public bool isDoubleSided { get; set; }
        [Key(6)]
        public bool isMirroredX { get; set; }
        [Key(7)]
        public bool isMirroredY { get; set; }
        [Key(8)]
        public bool isAudio3D { get; set; }
        [Key(9)]
        public bool hasAlphaSubsidary { get; set; }
        [Key(10)]
        public bool alphaEnabled { get; set; }
        [Key(11)]
        public string alphaURL { get; set; }
        [Key(12)]
        public int x { get; set; }
        [Key(13)]
        public int y { get; set; }
    }
    public class VideoPlatePlayer : MonoBehaviour
    {
        public GameObject plate;

        public Renderer renderer;

        public AudioSource audioSource;

        public RenderTexture rTex;

        public VideoPlayer player;

        public string url;

        public float volume = 0;

        public bool isTimelineMode;

        private int x;
        private int y;

        internal bool isInit = false;

        public float timelineStartOffset = 0.0f;

        private bool ultimateBodgeBool = false;
        private bool ultimateBodgeBool2 = false;    // used in logic to get firstVisibleFrameTime

        private double firstVisibleFrameTime = 0;

        private bool isMirroredX = false;
        private bool isMirroredY = false;
        public bool isDoubleSided = true;
        public bool isAudio3D = false;

        internal bool hasAlphaSubsidary = false;
        public bool alphaEnabled = false;
        private bool useMainForAlpha = false;

        private GameObject alphaSubsidary;
        private RenderTexture alphaRTex;
        internal VideoPlayer alphaPlayer;

        private Texture2D justWhite;

        void Awake()
        {
            justWhite = Texture2D.whiteTexture;
        }

        public void Init(GameObject plate_) //KK
        {

            plate = plate_;
            renderer = plate.GetComponentInChildren<MeshRenderer>();

            player = this.GetOrAddComponent<VideoPlayer>();

            player.renderMode = VideoRenderMode.RenderTexture;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;

            audioSource = this.GetOrAddComponent<AudioSource>();
            player.SetTargetAudioSource(0, audioSource);
            volume = 0.2f;
            audioSource.volume = 0.2f;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            audioSource.bypassListenerEffects = true;
            audioSource.ignoreListenerVolume = true;
            audioSource.dopplerLevel = 0f;
            player.sendFrameReadyEvents = true;

            player.prepareCompleted += setVideoParams; //set plate size and other parameters
            player.frameReady += frameReadyEvent; // used for the ultimate bodge

            isTimelineMode = false;
            isInit = true;
        }

        private void frameReadyEvent(VideoPlayer source, long frameIdx)
        {
            ultimateBodgeBool2 = true;
        }

        public void setVideo(string videoURL)
        {
            if (!isInit) return;

            // if player already has a video loaded, destroy it and make a new one, otherwise the game will crash
            if (player.url != "")
            {
                DestroyImmediate(this.GetComponent<VideoPlayer>());
                DestroyImmediate(player);
                player = null;
                float oldVolume = volume;
                Init(plate);
                volume = oldVolume;
                audioSource.volume = oldVolume;
            }

            url = videoURL;
            player.url = url;
            player.Prepare();
        }

        private void setVideoParams(VideoPlayer source)
        {
            if (!isInit) return;
            x = (int)player.texture.width;
            y = (int)player.texture.height;
            if (x != 0 && y != 0)
                VideoPlate.Logger.LogInfo($"Loaded video: [{url}]");
            else
                VideoPlate.Logger.LogWarning($"Error loading video, please check if the specified path/url is valid and the format is supported.\n Supported formats:\n mp4 | m4v | avi | mov");
            if (rTex != null)
            {
                DestroyImmediate(rTex);
            }
            rTex = new RenderTexture(x, y, 0);
            rTex.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            player.targetTexture = rTex;
            renderer.material.mainTexture = rTex;
            plate.GetComponentInChildren<MeshFilter>().mesh = newPlateMesh(x, y, isDoubleSided);
            audioSource.minDistance = 10;
            ultimateBodgeBool = true;
            //ultimateBodgeBool2 = false;
        }

        public void initAlphaSubsidary()
        {
            if (!isInit) return;
            alphaSubsidary = new GameObject();
            alphaPlayer = alphaSubsidary.AddComponent<VideoPlayer>();
            if (alphaRTex != null)
            {
                DestroyImmediate(alphaRTex);
            }
            alphaRTex = new RenderTexture(x, y, 0);
            alphaPlayer.audioOutputMode = VideoAudioOutputMode.None;
            alphaPlayer.targetTexture = alphaRTex;
            alphaPlayer.renderMode = VideoRenderMode.RenderTexture;
            alphaPlayer.playOnAwake = false;
            alphaPlayer.skipOnDrop = true;
            if (renderer.material.GetTexture("_AlphaMask") == null)
            {
                VideoPlate.Logger.LogWarning("VideoPlate Material does not have an AlphaMask channel, chaning material...");
                bool materialChanged = MaterialAPI.SetShader(renderer.gameObject, "m_koi_stu_kihon01_02", "Shader Forge/main_alpha");
                if (!materialChanged)
                {
                    VideoPlate.Logger.LogError("Changing material failed");
                    return;
                }
                else VideoPlate.Logger.LogWarning("Material changed to 'Shader Forge/main_alpha'");
            }
            alphaPlayer.prepareCompleted += setAlphaVideoParams;
            hasAlphaSubsidary = true;
        }

        public void setAlphaVideo(string videoURL)
        {
            if (!isInit) return;
            if (videoURL == "")
            {
                renderer.material.SetTexture("_AlphaMask", rTex);
                useMainForAlpha = true;
                return;
            }
            if (alphaSubsidary == null || alphaPlayer == null || alphaRTex == null)
                return;
            if (alphaPlayer.url != "")
            {
                GameObject.Destroy(alphaSubsidary);
                alphaPlayer = null;
                alphaSubsidary = null;
                initAlphaSubsidary();
            }
            alphaPlayer.url = videoURL;
            VideoPlate.Logger.LogInfo($"Loading video [{videoURL}] as AlphaMask...");
            alphaPlayer.Prepare();
        }

        private void alphaOnPlay(VideoPlayer source)
        {
            source.time = player.time;
        }

        private void setAlphaVideoParams(VideoPlayer source)
        {
            if (alphaPlayer.frameCount != player.frameCount)
            {
                VideoPlate.Logger.LogWarning("AlphaMask video does not match the framecount of the plate!");
                VideoPlate.Logger.LogMessage("WARNING: AlphaMask video does not match the framecount of the plate!");
            }
            useMainForAlpha = false;
            renderer.material.SetTexture("_AlphaMask", alphaRTex);
            if (alphaPlayer.texture.width == 0 || alphaPlayer.texture.height == 0)
                VideoPlate.Logger.LogWarning($"Error loading video, please check if the specified path/url is valid and the format is supported.\n Supported formats:\n mp4 | m4v | avi | mov");
            else
                VideoPlate.Logger.LogInfo("... AlphaMask loaded successfully!");
            trySetTime(player.time);
        }

        public void toggleAlphaMask()
        {
            if (!hasAlphaSubsidary || alphaSubsidary == null || alphaPlayer == null || alphaRTex == null) initAlphaSubsidary();
            alphaEnabled = !alphaEnabled;
            if (alphaEnabled)
            {
                if (useMainForAlpha) renderer.material.SetTexture("_AlphaMask", rTex);
                else renderer.material.SetTexture("_AlphaMask", alphaRTex);
            }
            else
            {
                renderer.material.SetTexture("_AlphaMask", justWhite);
            }
        }

        private void Update()
        {
            if (rTex == null) return;
            // get fristVisibleFrameTime
            if (ultimateBodgeBool && ultimateBodgeBool2)
            {
                PlayPause();
                ultimateBodgeBool = false;
                firstVisibleFrameTime = player.time + 1 / player.frameRate;
            }
        }

        public void setVolume(float newVolume)
        {
            if (!isInit) return;
            volume = newVolume;
            audioSource.volume = newVolume;
        }

        public void PlayPause()
        {
            if (!player.isPlaying)
            {
                player.Play();
                if (hasAlphaSubsidary)
                {
                    alphaPlayer.Play();
                    //alphaPlayer.time = player.time;
                }
            }
            else
            {
                player.Pause();
                if (hasAlphaSubsidary) alphaPlayer.Pause();
            }
        }

        public void nextFrame()
        {
            player.StepForward();
            if (hasAlphaSubsidary) alphaPlayer.StepForward();
        }
        public void prevFrame()
        {
            trySetTime(player.time - 1 / player.frameRate);
        }

        public void Stop()
        {
            //player.Stop();
            //player.Prepare();
            player.time = firstVisibleFrameTime;
            if (hasAlphaSubsidary) alphaPlayer.time = firstVisibleFrameTime;
            if (player.isPlaying)
            {
                player.Pause();
                if (hasAlphaSubsidary) alphaPlayer.Pause();
            }
        }

        public void setLoop(bool l)
        {
            player.isLooping = l;
            if (hasAlphaSubsidary) alphaPlayer.isLooping = l;
        }

        public bool trySetTime(double time)
        {
            if (player.canSetTime && time <= player.frameCount / player.frameRate && time >= 0)
            {
                if (time == 0)
                {
                    player.time = firstVisibleFrameTime;
                    if (hasAlphaSubsidary) alphaPlayer.time = firstVisibleFrameTime;
                    return true;
                }
                else
                {
                    player.time = time;
                    if (hasAlphaSubsidary) alphaPlayer.time = time;
                }
                return true;
            }
            else return false;
        }

        public void mirrorX()
        {
            flipMesh(plate.GetComponentInChildren<MeshFilter>().mesh, true, false);
            isMirroredX = !isMirroredX;
        }
        public void mirrorY()
        {
            flipMesh(plate.GetComponentInChildren<MeshFilter>().mesh, false, true);
            isMirroredY = !isMirroredY;
        }
        public void setDoubleSided(bool doubleSided)
        {
            plate.GetComponentInChildren<MeshFilter>().mesh = newPlateMesh(x, y, doubleSided);
            if (isMirroredX) mirrorX();
            if (isMirroredY) mirrorY();
        }

        public void setAudio3D(bool audio3D)
        {
            if (audio3D)
            {
                audioSource.spatialBlend = 1f;
                audioSource.spatialize = true;
                isAudio3D = true;
            }
            else
            {
                audioSource.spatialize = false;
                audioSource.spatialBlend = 0f;
                isAudio3D = false;
            }
        }

        private Mesh flipMesh(Mesh mesh, bool onX, bool onY)
        {
            if (!onX && !onY) return mesh;
            Vector3[] baseVertices = mesh.vertices;
            var vertices = new Vector3[baseVertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var vertex = baseVertices[i];
                if (onX) vertex.x = -vertex.x;
                if (onY) vertex.y = -vertex.y;
                vertices[i] = vertex;
            }
            mesh.vertices = vertices;
            if (onX ^ onY) // flip triangles if one but not both axes are flipped.
            {
                int[] baseTriangles = mesh.triangles;
                int[] triangles = new int[baseTriangles.Length];
                for (int x = 0; x < baseTriangles.Length; x += 3)
                {
                    triangles[x] = baseTriangles[x];
                    triangles[x + 1] = baseTriangles[x + 2];
                    triangles[x + 2] = baseTriangles[x + 1];
                }
                mesh.triangles = triangles;
            }
            return mesh;
        }

        private Mesh newPlateMesh(int x, int y, bool doubleSided = false)
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[4];
            Vector2[] uv = new Vector2[4];
            int[] triangles = new int[doubleSided ? 12 : 6];

            vertices[0] = new Vector3(-(0.001f * x / 2), +(0.001f * y / 2));
            vertices[1] = new Vector3(+(0.001f * x / 2), +(0.001f * y / 2));
            vertices[2] = new Vector3(-(0.001f * x / 2), -(0.001f * y / 2));
            vertices[3] = new Vector3(+(0.001f * x / 2), -(0.001f * y / 2));

            uv[0] = new Vector2(1, 1);
            uv[1] = new Vector2(0, 1);
            uv[2] = new Vector2(1, 0);
            uv[3] = new Vector2(0, 0);

            triangles[0] = 2;
            triangles[1] = 1;
            triangles[2] = 0;
            triangles[3] = 3;
            triangles[4] = 1;
            triangles[5] = 2;
            isDoubleSided = false;
            if (doubleSided)
            {
                triangles[6] = 0;
                triangles[7] = 1;
                triangles[8] = 2;
                triangles[9] = 2;
                triangles[10] = 1;
                triangles[11] = 3;
                isDoubleSided = true;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            mesh = flipMesh(mesh, isMirroredX, isMirroredY);

            return mesh;
        }

        void OnDestroy()
        {
            VideoPlate.playerItems.Remove(this);
        }

        public VideoPlatePlayerData getPlayerData()
        {
            VideoPlatePlayerData vppd = new VideoPlatePlayerData();
            vppd.url = url;
            vppd.volume = volume;
            vppd.isTimelineMode = isTimelineMode;
            vppd.timelineStartOffset = timelineStartOffset;
            vppd.isLooping = player.isLooping;
            vppd.isMirroredX = isMirroredX;
            vppd.isMirroredY = isMirroredY;
            vppd.isDoubleSided = isDoubleSided;
            vppd.isAudio3D = isAudio3D;
            vppd.x = x;
            vppd.y = y;
            if (hasAlphaSubsidary)
            {
                vppd.alphaURL = alphaPlayer.url;
                vppd.hasAlphaSubsidary = hasAlphaSubsidary;
            }
            vppd.alphaEnabled = alphaEnabled;
            return vppd;
        }

        public bool setPlayerData(VideoPlatePlayerData data)
        {
            if (isInit)
            {
                try
                {
                    x = data.x;
                    y = data.y;
                    isDoubleSided = data.isDoubleSided;
                    isMirroredX = data.isMirroredX;
                    isMirroredY = data.isMirroredY;
                    setVideo(data.url);
                    setVolume(data.volume);
                    setLoop(data.isLooping);
                    timelineStartOffset = data.timelineStartOffset;
                    isTimelineMode = data.isTimelineMode;
                    if (data.isTimelineMode && !VideoPlate.plateWithTimelinemodeExists)
                        VideoPlate.plateWithTimelinemodeExists = true;
                    if (!data.isAudio3D) setAudio3D(false);
                    if (data.hasAlphaSubsidary)
                    {
                        initAlphaSubsidary();
                        setAlphaVideo(data.alphaURL);
                    }
                    alphaEnabled = data.alphaEnabled;
                    return true;
                }
                catch
                {
                    VideoPlate.Logger.LogError("Loading Videoplate failed");
                    return false;
                }
            }
            else return false;
        }
    }
}
