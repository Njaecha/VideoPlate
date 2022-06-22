using System.Collections.Generic;
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using MessagePack;

namespace VideoPlate
{
    class SceneController : SceneCustomFunctionController
    {
        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();
            Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;

            List<byte[]> players = new List<byte[]>();
            List<int> items = new List<int>();

            foreach(VideoPlatePlayer vpp in VideoPlate.playerItems.Keys)
            {
                players.Add(MessagePackSerializer.Serialize<VideoPlatePlayerData>(vpp.getPlayerData()));
                items.Add(VideoPlate.playerItems[vpp].objectInfo.dicKey);
            }
            data.data.Add("players", MessagePackSerializer.Serialize<List<byte[]>>(players));
            data.data.Add("items", MessagePackSerializer.Serialize<List<int>>(items));
            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            var data = GetExtendedData();
            if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
            {
                VideoPlate.playerItems.Clear();
                VideoPlate.plateIndex = 0;
            }
            if (data == null || operation == SceneOperationKind.Clear) return;

            List<byte[]> playersSerialised = new List<byte[]>();
            List<int> items = new List<int>();

            if(data.data.TryGetValue("players", out var playersSerialised_)&& playersSerialised_ != null)
            {
                playersSerialised = MessagePackSerializer.Deserialize<List<byte[]>>((byte[])playersSerialised_);
            }
            if(data.data.TryGetValue("items", out var itemsSerialised) && itemsSerialised != null)
            {
                items = MessagePackSerializer.Deserialize<List<int>>((byte[])itemsSerialised);
            }
            List<VideoPlatePlayerData> players = new List<VideoPlatePlayerData>();
            foreach(byte[] bytes in playersSerialised)
            {
                players.Add(MessagePackSerializer.Deserialize<VideoPlatePlayerData>(bytes));
            }
            for( int i = 0; i < players.Count; i++)
            {
                OCIItem item = (OCIItem)loadedItems[items[i]];
                VideoPlatePlayerData vppd = players[i];

                VideoPlatePlayer vpp = VideoPlate.addVideoPlate(item);
                vpp.setPlayerData(vppd);
            }
        }
        protected override void OnObjectsCopied(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            Dictionary<int, ObjectCtrlInfo> sceneObjects = Studio.Studio.Instance.dicObjectCtrl;
            foreach (int id in copiedItems.Keys)
            {
                if (copiedItems[id] is OCIItem)
                {
                    OCIItem newItem = (OCIItem)copiedItems[id];
                    OCIItem oldItem = (OCIItem)sceneObjects[id];
                    if (VideoPlate.playerItems.ContainsValue(oldItem))
                    {
                        VideoPlatePlayer vpp = oldItem.objectItem.GetComponent<VideoPlatePlayer>();
                        if (vpp != null)
                        {
                            VideoPlatePlayer vpp2 = VideoPlate.addVideoPlate(newItem);
                            vpp2.setPlayerData(vpp.getPlayerData());
                            VideoPlate.setGuiContent();
                        }
                    }
                }
            }
        }
    }
}
