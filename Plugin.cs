﻿using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using SPT.Common.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static RealismMod.Attributes;
using static RealismMod.HazardZoneSpawner;
using static RootMotion.FinalIK.IKSolver;


namespace RealismMod
{
    public class RealismConfig
    {
        public bool recoil_attachment_overhaul { get; set; }
        public bool malf_changes { get; set; }
        public bool realistic_ballistics { get; set; }
        public bool med_changes { get; set; }
        public bool headset_changes { get; set; }
        public bool enable_stances { get; set; }
        public bool movement_changes { get; set; }
        public bool gear_weight { get; set; }
        public bool reload_changes { get; set; }
        public bool manual_chambering { get; set; }
        public bool food_changes { get; set; }
        public bool enable_hazard_zones { get; set; }   
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, Plugin.PLUGINVERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PLUGINVERSION = "1.4.4";

        public static Dictionary<Enum, Sprite> IconCache = new Dictionary<Enum, Sprite>();
        public static Dictionary<string, AudioClip> HitAudioClips = new Dictionary<string, AudioClip>();
        public static Dictionary<string, AudioClip> GasMaskAudioClips = new Dictionary<string, AudioClip>();
        public static Dictionary<string, AudioClip> HazardZoneClips = new Dictionary<string, AudioClip>();
        public static Dictionary<string, AudioClip> DeviceAudioClips = new Dictionary<string, AudioClip>();
        public static Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
        public static Dictionary<string, Texture> LoadedTextures = new Dictionary<string, Texture>();

        public static RealismConfig ServerConfig;

        private string _baseBundleFilepath;

        private float _realDeltaTime = 0f;
        public static float FPS = 1f;

        //mounting UI
        public static GameObject MountingUIGameObject { get; private set; }
        public MountingUI MountingUIComponent;

        //health controller
        public static RealismHealthController RealHealthController;

        //explosion
        public static UnityEngine.Object ExplosionPrefab { get; private set; }

        //weather controller
        public static GameObject RealismWeatherGameObject { get; private set; }
        public RealismWeatherController RealismWeatherComponent;

        public static bool HasReloadedAudio = false;
        public static bool FikaPresent = false;
        public static bool FOVFixPresent = false;
        private bool _detectedMods = false;

        public static bool StartRechamberTimer = false;
        public static float ChamberTimer = 0f;
        public static bool CanLoadChamber = false;
        public static bool BlockChambering = false;

        public static string CurrentProfileId = string.Empty;
        public static string PMCProfileId = string.Empty;
        public static string ScavProfileId = string.Empty;
        private bool _gotProfileId = false;

        private void LoadConfig()
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            try
            {
                var jsonString = RequestHandler.GetJson("/RealismMod/GetInfo");
                ServerConfig = JsonConvert.DeserializeObject<RealismConfig>(jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError($"REALISM MOD ERROR: FAILED TO FETCH CONFIG DATA FROM SERVER: {ex.Message}");
            }
        }

        private async void CacheIcons()
        {
            IconCache.Add(ENewItemAttributeId.ShotDispersion, Resources.Load<Sprite>("characteristics/icons/Velocity"));
            IconCache.Add(ENewItemAttributeId.BluntThroughput, Resources.Load<Sprite>("characteristics/icons/armorMaterial"));
            IconCache.Add(ENewItemAttributeId.VerticalRecoil, Resources.Load<Sprite>("characteristics/icons/Ergonomics"));
            IconCache.Add(ENewItemAttributeId.HorizontalRecoil, Resources.Load<Sprite>("characteristics/icons/Recoil Back"));
            IconCache.Add(ENewItemAttributeId.Dispersion, Resources.Load<Sprite>("characteristics/icons/Velocity"));
            IconCache.Add(ENewItemAttributeId.CameraRecoil, Resources.Load<Sprite>("characteristics/icons/SightingRange"));
            IconCache.Add(ENewItemAttributeId.AutoROF, Resources.Load<Sprite>("characteristics/icons/bFirerate"));
            IconCache.Add(ENewItemAttributeId.SemiROF, Resources.Load<Sprite>("characteristics/icons/bFirerate"));
            IconCache.Add(ENewItemAttributeId.ReloadSpeed, Resources.Load<Sprite>("characteristics/icons/weapFireType"));
            IconCache.Add(ENewItemAttributeId.FixSpeed, Resources.Load<Sprite>("characteristics/icons/icon_info_raidmoddable"));
            IconCache.Add(ENewItemAttributeId.ChamberSpeed, Resources.Load<Sprite>("characteristics/icons/icon_info_raidmoddable"));
            IconCache.Add(ENewItemAttributeId.AimSpeed, Resources.Load<Sprite>("characteristics/icons/SightingRange"));
            IconCache.Add(ENewItemAttributeId.Firerate, Resources.Load<Sprite>("characteristics/icons/bFirerate"));
            IconCache.Add(ENewItemAttributeId.Damage, Resources.Load<Sprite>("characteristics/icons/icon_info_bulletspeed"));
            IconCache.Add(ENewItemAttributeId.Penetration, Resources.Load<Sprite>("characteristics/icons/armorClass"));
            IconCache.Add(ENewItemAttributeId.BallisticCoefficient, Resources.Load<Sprite>("characteristics/icons/SightingRange"));
            IconCache.Add(ENewItemAttributeId.ArmorDamage, Resources.Load<Sprite>("characteristics/icons/armorMaterial"));
            IconCache.Add(ENewItemAttributeId.FragmentationChance, Resources.Load<Sprite>("characteristics/icons/icon_info_bloodloss"));
            IconCache.Add(ENewItemAttributeId.MalfunctionChance, Resources.Load<Sprite>("characteristics/icons/icon_info_raidmoddable"));
            IconCache.Add(ENewItemAttributeId.CanSpall, Resources.Load<Sprite>("characteristics/icons/icon_info_bulletspeed"));
            IconCache.Add(ENewItemAttributeId.SpallReduction, Resources.Load<Sprite>("characteristics/icons/Velocity"));
            IconCache.Add(ENewItemAttributeId.GearReloadSpeed, Resources.Load<Sprite>("characteristics/icons/weapFireType"));
            IconCache.Add(ENewItemAttributeId.CantADS, Resources.Load<Sprite>("characteristics/icons/SightingRange"));
            IconCache.Add(ENewItemAttributeId.CanADS, Resources.Load<Sprite>("characteristics/icons/SightingRange"));
            IconCache.Add(ENewItemAttributeId.NoiseReduction, Resources.Load<Sprite>("characteristics/icons/icon_info_loudness"));
            IconCache.Add(ENewItemAttributeId.ProjectileCount, Resources.Load<Sprite>("characteristics/icons/icon_info_bulletspeed"));
            IconCache.Add(ENewItemAttributeId.Convergence, Resources.Load<Sprite>("characteristics/icons/Ergonomics"));
            IconCache.Add(ENewItemAttributeId.HBleedType, Resources.Load<Sprite>("characteristics/icons/icon_info_bloodloss"));
            IconCache.Add(ENewItemAttributeId.LimbHpPerTick, Resources.Load<Sprite>("characteristics/icons/icon_info_bloodloss"));
            IconCache.Add(ENewItemAttributeId.HpPerTick, Resources.Load<Sprite>("characteristics/icons/hpResource"));
            IconCache.Add(ENewItemAttributeId.RemoveTrnqt, Resources.Load<Sprite>("characteristics/icons/hpResource"));
            IconCache.Add(ENewItemAttributeId.Comfort, Resources.Load<Sprite>("characteristics/icons/Weight"));
            IconCache.Add(ENewItemAttributeId.GasProtection, Resources.Load<Sprite>("characteristics/icons/icon_info_intoxication"));
            IconCache.Add(ENewItemAttributeId.RadProtection, Resources.Load<Sprite>("characteristics/icons/icon_info_radiation"));
            IconCache.Add(ENewItemAttributeId.PainKillerStrength, Resources.Load<Sprite>("characteristics/icons/hpResource"));
            IconCache.Add(ENewItemAttributeId.MeleeDamage, Resources.Load<Sprite>("characteristics/icons/icon_info_bloodloss"));
            IconCache.Add(ENewItemAttributeId.MeleePen, Resources.Load<Sprite>("characteristics/icons/icon_info_bulletspeed"));
            IconCache.Add(ENewItemAttributeId.OutOfRaidHP, Resources.Load<Sprite>("characteristics/icons/hpResource"));
            IconCache.Add(ENewItemAttributeId.StimType, Resources.Load<Sprite>("characteristics/icons/hpResource"));
            IconCache.Add(ENewItemAttributeId.DurabilityBurn, Resources.Load<Sprite>("characteristics/icons/Velocity"));
            IconCache.Add(ENewItemAttributeId.Heat, Resources.Load<Sprite>("characteristics/icons/Velocity"));
            IconCache.Add(ENewItemAttributeId.MuzzleFlash, Resources.Load<Sprite>("characteristics/icons/Velocity"));

            Sprite balanceSprite = await RequestResource<Sprite>(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\icons\\balance.png");
            Sprite recoilAngleSprite = await RequestResource<Sprite>(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\icons\\recoilAngle.png");
            IconCache.Add(ENewItemAttributeId.Balance, balanceSprite);
            IconCache.Add(ENewItemAttributeId.RecoilAngle, recoilAngleSprite);
        }

        private void LoadTextures()
        {
            string[] texFilesDir = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\masks\\", "*.png");

            foreach (string fileDir in texFilesDir)
            {
                LoadTexture(fileDir);
            }
        }

        private void LoadSprites()
        {
            string[] iconFilesDir = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\icons\\", "*.png");

            foreach (string fileDir in iconFilesDir)
            {
                LoadSprite(fileDir);
            }
        }

        private async void LoadSprite(string path)
        {
            LoadedSprites[Path.GetFileName(path)] = await RequestResource<Sprite>(path);
        }

        private async void LoadTexture(string path)
        {
            LoadedTextures[Path.GetFileName(path)] = await RequestResource<Texture>(path, true);
        }

        private async Task<T> RequestResource<T>(string path, bool isMask = false) where T : class
        {
            UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(path);
            UnityWebRequestAsyncOperation sendWeb = uwr.SendWebRequest();

            while (!sendWeb.isDone)
                await Task.Yield();

            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Logger.LogError("Realism Mod: Failed To Fetch Resource");
                return null;
            }
            else
            {
                Texture2D texture = ((DownloadHandlerTexture)uwr.downloadHandler).texture;

                if (typeof(T) == typeof(Texture))
                {
                    if (isMask)
                    {
                        Texture2D flipped = new Texture2D(texture.width, texture.height);
                        for (int y = 0; y < texture.height; y++)
                        {
                            for (int x = 0; x < texture.width; x++)
                            {
                                flipped.SetPixel(x, texture.height - y - 1, texture.GetPixel(x, y));
                            }
                        }
                        flipped.Apply();
                        texture = flipped;
                    }
                    return texture as T;
                }
                else if (typeof(T) == typeof(Sprite))
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    return sprite as T;
                }
                else
                {
                    Logger.LogError("Realism Mod: Unsupported resource type requested");
                    return null;
                }
            }
        }

        private async void LoadAudioClips()
        {
            string[] hitSoundsDir = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\sounds\\hitsounds");
            string[] gasMaskDir = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\sounds\\gasmask");
            string[] hazardDir = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\sounds\\zones");
            string[] deviceDir = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Realism\\sounds\\devices");

            HitAudioClips.Clear();
            GasMaskAudioClips.Clear();
            HazardZoneClips.Clear();
            DeviceAudioClips.Clear();

            foreach (string fileDir in hitSoundsDir)
            {
                HitAudioClips[Path.GetFileName(fileDir)] = await RequestAudioClip(fileDir);
            }
            foreach (string fileDir in gasMaskDir)
            {
                GasMaskAudioClips[Path.GetFileName(fileDir)] = await RequestAudioClip(fileDir);
            }
            foreach (string fileDir in hazardDir)
            {
                HazardZoneClips[Path.GetFileName(fileDir)] = await RequestAudioClip(fileDir);
            }
            foreach (string fileDir in deviceDir)
            {
                DeviceAudioClips[Path.GetFileName(fileDir)] = await RequestAudioClip(fileDir);
            }

            Plugin.HasReloadedAudio = true;
        }

        private async Task<AudioClip> RequestAudioClip(string path)
        {
            string extension = Path.GetExtension(path);
            AudioType audioType = AudioType.WAV;
            switch (extension)
            {
                case ".wav":
                    audioType = AudioType.WAV;
                    break;
                case ".ogg":
                    audioType = AudioType.OGGVORBIS;
                    break;
            }
            UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, audioType);
            UnityWebRequestAsyncOperation sendWeb = uwr.SendWebRequest();

            while (!sendWeb.isDone)
                await Task.Yield();

            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Logger.LogError("Realism Mod: Failed To Fetch Audio Clip");
                return null;
            }
            else
            {
                AudioClip audioclip = DownloadHandlerAudioClip.GetContent(uwr);
                return audioclip;
            }
        }

        private T LoadAndInitializePrefabs<T>(string bundlePath, string assetName) where T: UnityEngine.Object
        {
            string fullPath = Path.Combine(_baseBundleFilepath, bundlePath);
            AssetBundle bundle = AssetBundle.LoadFromFile(fullPath);

            if (bundle == null)
            {
                Logger.LogError($"Failed to load AssetBundle from {fullPath}");
                return null;
            }

            T asset = bundle.LoadAsset<T>(assetName);

            if (asset == null)
            {
                Logger.LogError($"Failed to load asset {assetName} from bundle {fullPath}");
            }

            return asset;
        }

        private void LoadBundles() 
        {
            _baseBundleFilepath = Path.Combine(Environment.CurrentDirectory, "BepInEx\\plugins\\Realism\\bundles\\");

            Assets.GooBarrel = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\goo_barrel.bundle", "Assets/Labs/yellow_barrel.prefab");
            Assets.BlueBox = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\bluebox.bundle", "Assets/Prefabs/bluebox.prefab");
            Assets.RedForkLift = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\redforklift.bundle", "Assets/Prefabs/autoloader.prefab");
            Assets.ElectroForkLift = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\electroforklift.bundle", "Assets/Prefabs/electroCar (2).prefab");
            Assets.BigForkLift = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\bigforklift.bundle", "Assets/Prefabs/loader (3).prefab");
            Assets.LabsCrate = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\labscrate.bundle", "Assets/Prefabs/woodBox_medium.prefab");
            Assets.Ural = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\ural.bundle", "Assets/Prefabs/ural280_closed_update.prefab");
            Assets.BluePallet = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\bluepallet.bundle", "Assets/Prefabs/pallete_plastic_blue (10).prefab");
            Assets.BlueFuelPalletCloth = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\bluebarrelpalletcloth.bundle", "Assets/Prefabs/pallet_barrel_heap_update.prefab");
            Assets.BarrelPile = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\barrelpile.bundle", "Assets/Prefabs/barrel_pile (1).prefab");
            Assets.LabsCrateSmall = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\labscratesmall.bundle", "Assets/Prefabs/woodBox_small (2).prefab");
            Assets.YellowPlasticPallet = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\yellowbarrelpallet.bundle", "Assets/Prefabs/pallet_barrel_plastic_clear_P (4).prefab");
            Assets.YellowPlasticBarrel = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\yellowbarrel.bundle", "Assets/Prefabs/barrel_plastic1_yellow_clear (6).prefab");
            Assets.LabsSuit1 = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\labssuit1.bundle", "Assets/Prefabs/lab_ChemProtect_V2.prefab");
            Assets.LabsSuit2 = LoadAndInitializePrefabs<UnityEngine.Object>("hazard_assets\\labssuit2.bundle", "Assets/Prefabs/lab_ChemProtect_V3.prefab");

            ExplosionPrefab = LoadAndInitializePrefabs<UnityEngine.Object>("exp\\expl.bundle", "Assets/Explosion/Prefab/NUCLEAR_EXPLOSION.prefab");
        }

        private void LoadMountingUI()
        {
            MountingUIGameObject = new GameObject();
            MountingUIComponent = MountingUIGameObject.AddComponent<MountingUI>();
            DontDestroyOnLoad(MountingUIGameObject);
        }

        private void LoadWeatherController()
        {
            RealismWeatherGameObject = new GameObject();
            RealismWeatherComponent = RealismWeatherGameObject.AddComponent<RealismWeatherController>();
            DontDestroyOnLoad(RealismWeatherGameObject);
        }

        private void LoadHealthController()
        {
            DamageTracker dmgTracker = new DamageTracker();
            RealismHealthController healthController = new RealismHealthController(dmgTracker);
            RealHealthController = healthController;
        }

        void Awake()
        {
            Utils.Logger = Logger;
        
            try
            {
                HazardZoneData.DeserializeZoneData();
                LoadBundles();
                LoadConfig();
                LoadSprites();
                LoadTextures();
                LoadAudioClips();
                CacheIcons();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception);
            }

            LoadMountingUI();
            //LoadWeatherController();
            LoadHealthController();
            PluginConfig.InitConfigBindings(Config);

            MoveDaCube.InitTempBindings(Config); //TEMPORARY
            new GetAvailableActionsPatch().Enable(); //TEMPORARY



            LoadGeneralPatches();
            //hazards
            if (ServerConfig.enable_hazard_zones) 
            {
                LoadHazardPatches();
            }
            //malfunctions
            if (ServerConfig.malf_changes)
            {
                LoadMalfPatches();
            }

            //recoil and attachments
            if (ServerConfig.recoil_attachment_overhaul) 
            {
                LoadRecoilPatches();
            }

            LoadReloadPatches();
  
            //Ballistics
            if (ServerConfig.realistic_ballistics)
            {
                LoadBallisticsPatches();
            }

            //Deafen Effects
            if (ServerConfig.headset_changes)
            {
                LoadDeafenPatches();
            }

            //gear patces
            if (ServerConfig.gear_weight)
            {      
                new TotalWeightPatch().Enable();
            }

            //Movement
            if (ServerConfig.movement_changes) 
            {
                LoadMovementPatches();
            }

            //Stances
            if (ServerConfig.enable_stances) 
            {
                LoadStancePatches();
            }

            //also needed for visual recoil
            if (ServerConfig.enable_stances || ServerConfig.realistic_ballistics)
            {
                new ApplyComplexRotationPatch().Enable(); 
            }

            if (ServerConfig.enable_stances || ServerConfig.headset_changes) 
            {
                new ADSAudioPatch().Enable();
            }

            //Health
            if (ServerConfig.med_changes)
            {
                LoadMedicalPatches();
            }

            //needed for food and meds
            if (ServerConfig.med_changes || ServerConfig.food_changes)
            {
                new ApplyItemStashPatch().Enable();
                new StimStackPatch1().Enable();
                new StimStackPatch2().Enable();
            }

        }

        private void CheckForMods() 
        {
            if (!_detectedMods && (int)Time.time % 5 == 0)
            {
                _detectedMods = true;
                if (Chainloader.PluginInfos.ContainsKey("com.fika.core"))
                {
                    FikaPresent = true;
                }
                if (Chainloader.PluginInfos.ContainsKey("FOVFix"))
                {
                    FOVFixPresent = true;
                }
            }
        }

        private void CheckForProfileID() 
        {
            //keep trying to get player profile id and update hazard values
            if (!_gotProfileId)
            {
                try
                {
                    PMCProfileId = Singleton<ClientApplication<ISession>>.Instance.GetClientBackEndSession().Profile.Id;
                    ScavProfileId = Singleton<ClientApplication<ISession>>.Instance.GetClientBackEndSession().ProfileOfPet.Id;
                    if(ServerConfig.enable_hazard_zones) HazardTracker.GetHazardValues(PMCProfileId);
                    _gotProfileId = true;
                }
                catch 
                {
                    if (PluginConfig.EnableLogging.Value) Logger.LogWarning("Realism Mod: Error Getting Profile ID, Retrying");
                }
            }
        }
   
        void Update()
        {
            //TEMPORARY
            MoveDaCube.Update();

            //games procedural animations are highly affected by FPS. I balanced everything at 144 FPS, so need to factor it.    
            _realDeltaTime += (Time.unscaledDeltaTime - _realDeltaTime) * 0.1f;
            FPS = 1.0f / _realDeltaTime;

            CheckForProfileID();
            CheckForMods();

            Utils.CheckIsReady();
            if (Utils.IsReady)
            {
                /*    if (GameWorldController.GameStarted && Input.GetKeyDown(KeyCode.N))
                    {
                        var player = Utils.GetYourPlayer().Transform;
                        Instantiate(ExplosionPrefab, new Vector3(1000f, 0f, 317f), new Quaternion(0, 0, 0, 0));
                    }*/

                if (HazardZoneSpawner.GameStarted && PluginConfig.ZoneDebug.Value)
                {

                    if (Input.GetKeyDown(KeyCode.Keypad1))
                    {
                        var player = Utils.GetYourPlayer().Transform;
                        Utils.LoadLoot(player.position, player.rotation, PluginConfig.TargetZone.Value);
                        Utils.Logger.LogWarning("\"position\": " + "\"x\":" + player.position.x + "," + "\"y\":" + player.position.y + "," + "\"z:\"" + player.position.z);
                        Utils.Logger.LogWarning("\"rotation\": " + "\"x\":" + player.rotation.eulerAngles.x + "," + "\"y\":" + player.eulerAngles.y + "," + "\"z:\"" + player.eulerAngles.z);
                    }
                }
                if (PluginConfig.ZoneDebug.Value && Input.GetKeyDown(KeyCode.Keypad0))
                {
                    HazardTracker.WipeTracker();
                }

                if (PluginConfig.ZoneDebug.Value && Input.GetKeyDown(PluginConfig.AddZone.Value.MainKey))
                {
                    DebugZones();
                }

                if (!Plugin.HasReloadedAudio)
                {
                    LoadAudioClips();
                    Plugin.HasReloadedAudio = true;
                }

                RecoilController.RecoilUpdate();

                if (ServerConfig.headset_changes)
                {
                    AudioControllers.HeadsetVolumeAdjust();
                    if (DeafeningController.PrismEffects != null)
                    {
                        DeafeningController.DoDeafening();
                    }
                }
                if (ServerConfig.enable_stances) 
                {
                    StanceController.StanceState();
                }
            }
            else
            {
                HasReloadedAudio = false;
            }
            if (ServerConfig.med_changes)
            {
                RealHealthController.ControllerUpdate();
            }
        }

        private void LoadGeneralPatches()
        {
            new BirdPatch().Enable();

            //misc
            new ChamberCheckUIPatch().Enable();

            //multiple
            new KeyInputPatch1().Enable();
            new KeyInputPatch2().Enable();
            new SyncWithCharacterSkillsPatch().Enable();
            new OnItemAddedOrRemovedPatch().Enable();
            new PlayerUpdatePatch().Enable();
            new PlayerInitPatch().Enable();
            new FaceshieldMaskPatch().Enable();
            new PlayPhrasePatch().Enable();
            new OnGameStartPatch().Enable();
            new OnGameEndPatch().Enable();
            new QuestCompletePatch().Enable();

            //stats used by multiple features
            new RigConstructorPatch().Enable();
            new EquipmentPenaltyComponentPatch().Enable();
        }

        private void LoadHazardPatches()
        {
            new HealthPanelPatch().Enable();
        }

        private void LoadMalfPatches()
        {
            new GetTotalMalfunctionChancePatch().Enable();
            new IsKnownMalfTypePatch().Enable();
            if (ServerConfig.manual_chambering)
            {
                new SetAmmoCompatiblePatch().Enable();
                new StartReloadPatch().Enable();
                new StartEquipWeapPatch().Enable();
                new SetAmmoOnMagPatch().Enable();
                new PreChamberLoadPatch().Enable();
            }
        }

        private void LoadRecoilPatches()
        {
            //procedural animations
            /*new CalculateCameraPatch().Enable();*/
            if (PluginConfig.EnableMuzzleEffects.Value) new MuzzleEffectsPatch().Enable();
            new UpdateWeaponVariablesPatch().Enable();
            new SetAimingSlowdownPatch().Enable();
            new PwaWeaponParamsPatch().Enable();
            new UpdateSwayFactorsPatch().Enable();
            new GetOverweightPatch().Enable();
            new SetOverweightPatch().Enable();
            new BreathProcessPatch().Enable();
            new CamRecoilPatch().Enable();

            //weapon and related
            new TotalShotgunDispersionPatch().Enable();
            new GetDurabilityLossOnShotPatch().Enable();
            new AutoFireRatePatch().Enable();
            new SingleFireRatePatch().Enable();
            new ErgoDeltaPatch().Enable();
            new ErgoWeightPatch().Enable();
            new PlayerErgoPatch().Enable();
            new ToggleAimPatch().Enable();
            new GetMalfunctionStatePatch().Enable();
            if (PluginConfig.EnableZeroShift.Value)
            {
                new CalibrationLookAt().Enable();
                new CalibrationLookAtScope().Enable();
            }
            //Stat Display Patches
            new ModConstructorPatch().Enable();
            new WeaponConstructorPatch().Enable();
            new HRecoilDisplayStringValuePatch().Enable();
            new HRecoilDisplayDeltaPatch().Enable();
            new VRecoilDisplayStringValuePatch().Enable();
            new VRecoilDisplayDeltaPatch().Enable();
            new ModVRecoilStatDisplayPatchFloat().Enable();
            new ModVRecoilStatDisplayPatchString().Enable();
            new ErgoDisplayDeltaPatch().Enable();
            new ErgoDisplayStringValuePatch().Enable();

            new FireRateDisplayStringValuePatch().Enable();

            new PenetrationUIPatch().Enable();

            new ModErgoStatDisplayPatch().Enable();
            new GetAttributeIconPatches().Enable();
            new MagazineMalfChanceDisplayPatch().Enable();
            new BarrelModClassPatch().Enable();
            new AmmoCaliberPatch().Enable();

            new COIDeltaPatch().Enable();
            new CenterOfImpactMOAPatch().Enable();
            new COIDisplayDeltaPatch().Enable();
            new COIDisplayStringValuePatch().Enable();
            new GetTotalCenterOfImpactPatch().Enable();

            //Recoil Patches
            //new GetCameraRotationRecoilPatch().Enable(); makes recoil feel iffy, doesn't seem needed
            new RecalcWeaponParametersPatch().Enable();
            new AddRecoilForcePatch().Enable();
            new RecoilAnglesPatch().Enable();
            new ShootPatch().Enable();
            new RotatePatch().Enable();
        }

        private void LoadReloadPatches()
        {
            //Reload Patches
            if (ServerConfig.reload_changes)
            {
                new CanStartReloadPatch().Enable();
                new ReloadMagPatch().Enable();
                new QuickReloadMagPatch().Enable();
                new SetMagTypeCurrentPatch().Enable();
                new SetMagTypeNewPatch().Enable();
                new SetMagInWeaponPatch().Enable();
                new SetMalfRepairSpeedPatch().Enable();
                new BoltActionReloadPatch().Enable();
                new SetWeaponLevelPatch().Enable();
            }

            if (ServerConfig.reload_changes || ServerConfig.recoil_attachment_overhaul || ServerConfig.enable_stances)
            {
                new ReloadWithAmmoPatch().Enable();
                new ReloadBarrelsPatch().Enable();
                new ReloadCylinderMagazinePatch().Enable();
                new OnMagInsertedPatch().Enable();
                new SetSpeedParametersPatch().Enable();
                new CheckAmmoPatch().Enable();
                new CheckChamberPatch().Enable();
                new RechamberPatch().Enable();
                new SetAnimatorAndProceduralValuesPatch().Enable();
            }
        }

        private void LoadStancePatches()
        {
            new ApplySimpleRotationPatch().Enable();
            new InitTransformsPatch().Enable();
            new ZeroAdjustmentsPatch().Enable();
            new WeaponOverlappingPatch().Enable();
            new WeaponLengthPatch().Enable();
            new OnWeaponDrawPatch().Enable();
            new UpdateHipInaccuracyPatch().Enable();
            new SetFireModePatch().Enable();
            new WeaponOverlapViewPatch().Enable();
            new CollisionPatch().Enable();
            new OperateStationaryWeaponPatch().Enable();
            new SetTiltPatch().Enable();
            new BattleUIScreenPatch().Enable();
            new ChangePosePatch().Enable();
            new MountingPatch().Enable();
            new ShouldMoveWeapCloserPatch().Enable();
        }

        private void LoadMedicalPatches()
        {
            new SetQuickSlotPatch().Enable();
            new ApplyItemPatch().Enable();
            new BreathIsAudiblePatch().Enable();
            new SetMedsInHandsPatch().Enable();
            new ProceedMedsPatch().Enable();
            new RemoveEffectPatch().Enable();
            new StaminaRegenRatePatch().Enable();
            new MedkitConstructorPatch().Enable();
            new HealthEffectsConstructorPatch().Enable();
            new HCApplyDamagePatch().Enable();
            new RestoreBodyPartPatch().Enable();
            new FlyingBulletPatch().Enable();
            new ToggleHeadDevicePatch().Enable();
            new HealCostDisplayShortPatch().Enable();
            new HealCostDisplayFullPatch().Enable();
        }

        private void LoadMovementPatches()
        {
            if (PluginConfig.EnableMaterialSpeed.Value)
            {
                new CalculateSurfacePatch().Enable();
            }
            new ClampSpeedPatch().Enable();
            new SprintAccelerationPatch().Enable();
            new EnduranceSprintActionPatch().Enable();
            new EnduranceMovementActionPatch().Enable();
        }

        private void LoadDeafenPatches()
        {
            new PrismEffectsEnablePatch().Enable();
            new PrismEffectsDisablePatch().Enable();
            new UpdatePhonesPatch().Enable();
            new SetCompressorPatch().Enable();
            new RegisterShotPatch().Enable();
            new ExplosionPatch().Enable();
            new GrenadeClassContusionPatch().Enable();
            new CovertMovementVolumePatch().Enable();
            new CovertMovementVolumeBySpeedPatch().Enable();
            new CovertEquipmentVolumePatch().Enable();
            new HeadsetConstructorPatch().Enable();
        }

        private void LoadBallisticsPatches()
        {
            /*new SetSkinPatch().Enable();*/
            /*new CollidersPatch().Enable();*/
            new InitiateShotPatch().Enable();
            new VelocityPatch().Enable();
            new CreateShotPatch().Enable();
            new ApplyArmorDamagePatch().Enable();
            new ApplyDamageInfoPatch().Enable();
            new SetPenetrationStatusPatch().Enable();
            new IsPenetratedPatch().Enable();
            new AfterPenPlatePatch().Enable();
            new IsShotDeflectedByHeavyArmorPatch().Enable();
            new ArmorLevelUIPatch().Enable();
            new ArmorLevelDisplayPatch().Enable();
            new ArmorClassStringPatch().Enable();
            new DamageInfoPatch().Enable();
            if (PluginConfig.EnableRagdollFix.Value) new ApplyCorpseImpulsePatch().Enable();
            new GetCachedReadonlyQualitiesPatch().Enable();
        }
    }
}



