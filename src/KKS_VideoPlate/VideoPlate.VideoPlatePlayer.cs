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
        internal bool isInit = false;

        // width and height
        private int x;
        private int y;

        // ultitamte bodge (fix VideoPlayser Stop issue)
        private bool ultimateBodgeBool = false;
        private double firstVisibleFrameTime = 0;

        // advanced plate settings
        public float timelineStartOffset = 0.0f;
        private bool isMirroredX = false;
        private bool isMirroredY = false;
        public bool isDoubleSided = true;
        public bool isAudio3D = false;

        // bools used to keep track of the alpha mask
        internal bool hasAlphaSubsidary = false;
        public bool alphaEnabled = false;
        private bool useMainForAlpha = false;

        // objects used for the alpha mask
        private GameObject alphaSubsidary;
        private RenderTexture alphaRTex;
        internal VideoPlayer alphaPlayer;

        private Texture2D justWhite;

        void Awake()
        {
            justWhite = Texture2D.whiteTexture;
        }

        /// <summary>
        /// Creates and Initializes the VideoPlayer and AudiosSource components.
        /// </summary>
        /// <param name="plate_">Gameobject which should become the videoplate</param>
        public void Init(GameObject plate_)
        {

            plate = plate_;
            renderer = plate.GetComponentInChildren<MeshRenderer>();

            player = this.GetOrAddComponent<VideoPlayer>();
            player.playOnAwake = false;
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
            

            player.prepareCompleted += setVideoParams;

            isTimelineMode = false;
            isInit = true;
        }

        /// <summary>
        /// gives the player a video to play
        /// </summary>
        /// <param name="videoURL">Filepath or URL of the video</param>
        public void setVideo(string videoURL)
        {
            if (!isInit) return;
            player.Stop();
            url = videoURL;
            player.url = url;
            player.Prepare();
        }

        /// <summary>
        /// Autocalled by the prepareCompleted event of the VideoPlayer
        /// Creates and applies the renderTexture and plateMesh
        /// </summary>
        /// <param name="source">player which called this method</param>
        private void setVideoParams(VideoPlayer source)
        {
            if (!isInit) return;
            x = (int)player.width;
            y = (int)player.height;
            if (x != 0 && y != 0)
                VideoPlate.Logger.LogInfo($"Loaded video: [{url}]");
            else
                VideoPlate.Logger.LogWarning($"Error loading video, please check if the specified path/url is valid and the format is supported.\n Supported formats:\n mp4 | m4v | avi | mov");
            if (rTex != null)
            {
                Destroy(rTex);
            }
            rTex = new RenderTexture(x, y, 0);
            rTex.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            player.targetTexture = rTex;
            renderer.material.mainTexture = rTex;
            plate.GetComponentInChildren<MeshFilter>().mesh = newPlateMesh(x, y, isDoubleSided);
            audioSource.minDistance = 10;
            ultimateBodgeBool = true;
            player.Play();
        }

        /// <summary>
        /// Creates and initializes the VideoPlayer for the alpha subsidary 
        /// </summary>
        public void initAlphaSubsidary()
        {
            if (!isInit) return;
            alphaSubsidary = new GameObject();
            alphaPlayer = alphaSubsidary.AddComponent<VideoPlayer>();
            alphaRTex = new RenderTexture(x, y, 0);
            alphaPlayer.audioOutputMode = VideoAudioOutputMode.None;
            alphaPlayer.targetTexture = alphaRTex;
            alphaPlayer.renderMode = VideoRenderMode.RenderTexture;
            alphaPlayer.playOnAwake = false;
            alphaPlayer.skipOnDrop = true;
            bool hasAlphaMask = false;
            foreach (string textureName in renderer.material.GetTexturePropertyNames())
                if (textureName == "_AlphaMaks") hasAlphaMask = true;
            if (!hasAlphaMask)
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

        /// <summary>
        /// Gives the alpha player a video to play
        /// </summary>
        /// <param name="videoURL">Filepath or URL of the video</param>
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
            alphaPlayer.url = videoURL;
            VideoPlate.Logger.LogInfo($"Loading video [{videoURL}] as AlphaMask...");
            alphaPlayer.Prepare();
        }

        /// <summary>
        /// Autocalled by the prepareCompleted event of the alpha VideoPlayer
        /// applies the rendertexture to the _AlphaMask channel
        /// </summary>
        /// <param name="source">player which called this method</param>
        private void setAlphaVideoParams(VideoPlayer source)
        {
            if (alphaPlayer.frameCount != player.frameCount)
            {
                VideoPlate.Logger.LogWarning("AlphaMask video does not match the framecount of the plate!");
                VideoPlate.Logger.LogMessage("WARNING: AlphaMask video does not match the framecount of the plate!");
            }
            useMainForAlpha = false;
            renderer.material.SetTexture("_AlphaMask", alphaRTex);
            if (alphaPlayer.width == 0 || alphaPlayer.height == 0)
                VideoPlate.Logger.LogWarning($"Error loading video, please check if the specified path/url is valid and the format is supported.\n Supported formats:\n mp4 | m4v | avi | mov");
            else
                VideoPlate.Logger.LogInfo("... AlphaMask loaded successfully!");
            trySetTime(player.time);
        }

        /// <summary>
        /// toggle the alpha mask mode
        /// </summary>
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
            if (ultimateBodgeBool && rTex.IsCreated())
            {
                player.Pause();
                ultimateBodgeBool = false;
                firstVisibleFrameTime = player.time;
            }
        }

        /// <summary>
        /// sets the plates audio volume
        /// </summary>
        /// <param name="newVolume">Volume between 0 and 1</param>
        public void setVolume(float newVolume)
        {
            if (!isInit) return;
            volume = newVolume;
            audioSource.volume = newVolume;
        }

        /// <summary>
        /// Plays or pauses the plates player
        /// </summary>
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

        /// <summary>
        /// Skips to the next frame of the video
        /// </summary>
        public void nextFrame()
        {
            player.StepForward();
            if (hasAlphaSubsidary) alphaPlayer.StepForward();
        }
        /// <summary>
        /// Skips to the previous frame of the video
        /// </summary>
        public void prevFrame()
        {
            trySetTime(player.time - 1 / player.frameRate);
        }

        /// <summary>
        /// Stops the plates player
        /// </summary>
        public void Stop()
        {
            //player.Stop();
            //player.Prepare();
            //setting the time to 0 does not update the displayes frame as of the nature of Unity's VideoPlayer
            player.time = firstVisibleFrameTime; 
            if (hasAlphaSubsidary) alphaPlayer.time = firstVisibleFrameTime;
            if (player.isPlaying)
            {
                player.Pause();
                if (hasAlphaSubsidary) alphaPlayer.Pause();
            }
        }

        /// <summary>
        /// Set looping to on of off
        /// </summary>
        /// <param name="l">On (true) or off (false)</param>
        public void setLoop(bool l)
        {
            player.isLooping = l;
            if (hasAlphaSubsidary) alphaPlayer.isLooping = l;
        }

        /// <summary>
        /// Attempts to set the playback time of the plates player
        /// </summary>
        /// <param name="time">Time to jump to</param>
        /// <returns>Returns false failed to set time</returns>
        public bool trySetTime(double time)
        {
            if (player.canSetTime && time <= player.length && time >= 0)
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

        /// <summary>
        /// Mirrors the plate vertically
        /// </summary>
        public void mirrorX()
        {
            flipMesh(plate.GetComponentInChildren<MeshFilter>().mesh, true, false);
            isMirroredX = !isMirroredX;
        }
        /// <summary>
        /// Mirros the plate horizontally
        /// </summary>
        public void mirrorY()
        {
            flipMesh(plate.GetComponentInChildren<MeshFilter>().mesh, false, true);
            isMirroredY = !isMirroredY;
        }
        /// <summary>
        /// Sets double Sided mode to on or off
        /// </summary>
        /// <param name="doubleSided">On (true) or off (false)</param>
        public void setDoubleSided(bool doubleSided)
        {
            plate.GetComponentInChildren<MeshFilter>().mesh = newPlateMesh(x, y, doubleSided);
            if (isMirroredX) mirrorX();
            if (isMirroredY) mirrorY();
        }

        /// <summary>
        /// Sets local (3D) audio to on or off
        /// </summary>
        /// <param name="audio3D">On (true) or off (false)</param>
        public void setAudio3D(bool audio3D)
        { 
            if( audio3D)
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

        /// <summary>
        /// Flips a mesh in specified directions
        /// </summary>
        /// <param name="mesh">Mesh to be flipped</param>
        /// <param name="onX">Flip on the X axis</param>
        /// <param name="onY">Flip on the Z axis</param>
        /// <returns>Flipped mesh</returns>
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

        /// <summary>
        /// Creates a new plateMesh
        /// </summary>
        /// <param name="x">video width</param>
        /// <param name="y">video height</param>
        /// <param name="doubleSided">is double sided (optional)</param>
        /// <returns>plateMesh</returns>
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

        /// <summary>
        /// Creates a VidepPlatePlayerData object and fills it with this plates current state
        /// </summary>
        /// <returns>this plates VideoPlatePlayerData</returns>
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

        /// <summary>
        /// Applies the data saved within a VideoPlatePlayerData object to this plate
        /// </summary>
        /// <param name="data">Data to apply</param>
        /// <returns>Returns false if failed to apply</returns>
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
