﻿using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using RainbowMage.OverlayPlugin;

namespace Tamagawa.EnmityPlugin
{
    [Serializable()]
    internal class ScanFailedException : Exception
    {
        private string message = String.Empty;
        public ScanFailedException() : base() { message = "Failed to signature scan"; }
        public ScanFailedException(string message) : base(message) { }
        public ScanFailedException(string message, System.Exception inner) : base(message, inner) { }
        protected ScanFailedException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) { }
    }

    public class EnmityOverlay : OverlayBase<EnmityOverlayConfig>
    {
        private const string charmapSignature32 = "81feffff0000743581fe58010000732d8b3cb5";
        private const string charmapSignature64 = "48c1e8033dffff0000742b3da80100007324488d0d";
        private const string targetSignature32  = "750e85d2750ab9";
        private const string targetSignature64  = "4883C4205FC3483935285729017520483935";
        private const int charmapOffset32 = 0;
        private const int charmapOffset64 = 0;
        private const int targetOffset32  = 88;
        private const int targetOffset64  = 0;
        private const int hateOffset32    = 19188; // TODO: should be more stable
        private const int hateOffset64    = 25312; // TODO: should be more stable

        private int pid = 0;
        private IntPtr charmapAddress = IntPtr.Zero;
        private IntPtr targetAddress = IntPtr.Zero;
        private IntPtr hateAddress = IntPtr.Zero;

        private bool suppress_log = false;

        public EnmityOverlay(EnmityOverlayConfig config) : base(config, config.Name)
        {
        }

        /// <summary>
        /// プロセスの変更をチェック
        /// </summary>
        private void checkProcessId()
        {
            try
            {
                if (FFXIVPluginHelper.Instance != null && FFXIVPluginHelper.GetFFXIVProcess != null)
                {
                    if (pid != FFXIVPluginHelper.GetFFXIVProcess.Id)
                    {
                        pid = FFXIVPluginHelper.GetFFXIVProcess.Id;
                        if (pid != 0)
                        {
                            getPointerAddress();
                            // スキャン間隔をもどす
                            timer.Interval = this.Config.ScanInterval;
                            suppress_log = false;
                        }
                    }
                }
                else
                {
                    pid = 0;
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, ex.ToString());
                pid = 0;
            }
        }

        /// <summary>
        /// 各ポインタのアドレスを取得 (基本的に一回でいい)
        /// </summary>
        private void getPointerAddress()
        {
            string charmapSignature = charmapSignature32;
            string targetSignature = targetSignature32;
            int targetOffset = targetOffset32;
            int hateOffset = hateOffset32;
            int charmapOffset = charmapOffset32;
            bool bRIP = false;

            if (FFXIVPluginHelper.GetFFXIVClientMode == FFXIVPluginHelper.FFXIVClientMode.FFXIV_64)
            {
                bRIP = true;
                hateOffset       = hateOffset64;
                targetOffset     = targetOffset64;
                charmapOffset    = charmapOffset64;
                targetSignature  = targetSignature64;
                charmapSignature = charmapSignature64;
            }

            /// CHARMAP
            List<IntPtr> list = FFXIVPluginHelper.SigScan(charmapSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                charmapAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                charmapAddress = list[0] + charmapOffset;
                hateAddress = charmapAddress + hateOffset;
            }
            if (charmapAddress == IntPtr.Zero)
            {
                throw new ScanFailedException();
            }

            /// TARGET
            list = FFXIVPluginHelper.SigScan(targetSignature, 0, bRIP);
            if (list == null || list.Count == 0)
            {
                targetAddress = IntPtr.Zero;
            }
            if (list.Count == 1)
            {
                targetAddress = list[0] + targetOffset;
            }
            if (targetAddress == IntPtr.Zero)
            {
                throw new ScanFailedException();
            }

            { 
                LogLevel level = LogLevel.Debug;
                if (Name.Equals("EnmityDebug")) {
                    level = LogLevel.Info;
                }
                Log(level, "Charmap Address: 0x{0:X}, HateStructure: 0x{1:X}", charmapAddress.ToInt64(), hateAddress.ToInt64());
                Log(level, "Target Address: 0x{0:X}", targetAddress.ToInt64());
            }
        }

        //public override void Navigate(string url)
        //{
        //    base.Navigate(url);
        //}

        protected override void Update()
        {
            try
            {
                checkProcessId(); // プロセスチェック
                if (pid == 0)
                {
                    if (suppress_log == false)
                    {
                        Log(LogLevel.Warning, Messages.ProcessNotFound);
                        suppress_log = true;
                    }
                    // スキャン間隔を一旦遅くする
                    timer.Interval = 3000;
                    // return; 一応表示するので戻らない
                }

                var updateScript = CreateEventDispatcherScript();

                if (this.Overlay != null &&
                    this.Overlay.Renderer != null &&
                    this.Overlay.Renderer.Browser != null)
                {
                    this.Overlay.Renderer.Browser.GetMainFrame().ExecuteJavaScript(updateScript, null, 0);
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Update: {1}", this.Name, ex);
            }
        }

        /// <summary>
        /// データを取得し、JSONを作る
        /// </summary>
        /// <returns></returns>
        internal string CreateJsonData()
        {
            /// シリアライザ
            var serializer = new JavaScriptSerializer();
            /// Overlay に渡すオブジェクト
            EnmityObject enmity = new EnmityObject();
            enmity.Entries = new List<EnmityEntry>();
            IntPtr currentTarget;

            //// なんかプロセスがおかしいとき
            if (pid == 0)
            {
                enmity.Target = new Combatant() {
                    Name = "Failed to scan memory.",
                    ID = 0,
                    MaxHP = 0,
                    CurrentHP = 0,
                    Distance = "0.00",
                    EffectiveDistance = 0,
                    HorizontalDistance = "0.00"
                };
                return serializer.Serialize(enmity);
            }

            var targetInfoSource = FFXIVPluginHelper.GetByteArray(targetAddress, 128);
            unsafe
            {
                if (FFXIVPluginHelper.GetFFXIVClientMode == FFXIVPluginHelper.FFXIVClientMode.FFXIV_64)
                {
                    fixed (byte* bp = targetInfoSource) currentTarget = new IntPtr(*(Int64*)bp);
                }
                else
                {
                    fixed (byte* bp = targetInfoSource) currentTarget = new IntPtr(*(Int32*)bp);

                }
            }
            /// なにもターゲットしてない
            if (currentTarget.ToInt64() <= 0)
            {
                enmity.Target = null;
                return serializer.Serialize(enmity);
            }

            try
            {
                /// 自キャラ
                IntPtr address = (IntPtr)FFXIVPluginHelper.GetUInt32(charmapAddress);
                var source =  FFXIVPluginHelper.GetByteArray(address, 0x3F40);
                Combatant mypc = FFXIVPluginHelper.GetCombatantFromByteArray(source);

                /// カレントターゲット
                source = FFXIVPluginHelper.GetByteArray(currentTarget, 0x3F40);
                enmity.Target = FFXIVPluginHelper.GetCombatantFromByteArray(source);

                /// 距離計算
                enmity.Target.Distance = mypc.GetDistanceTo(enmity.Target).ToString("0.00");
                enmity.Target.HorizontalDistance = mypc.GetHorizontalDistanceTo(enmity.Target).ToString("0.00");

                if (enmity.Target.type == TargetType.Monster)
                {
                    /// 周辺の戦闘キャラリスト(IDからNameを取得するため)
                    List<Combatant> combatantList = FFXIVPluginHelper.GetCombatantList(charmapAddress);

                    /// 一度に全部読む
                    byte[] buffer = FFXIVPluginHelper.GetByteArray(hateAddress, 16 * 72);
                    uint TopEnmity = 0;
                    ///
                    for (int i = 0; i < 16; i++ )
                    {
                        int p = i * 72;
                        uint _id;
                        uint _enmity;

                        unsafe
                        {
                            fixed (byte* bp = buffer)
                            {
                                _id     = *(uint*)&bp[p];
                                _enmity = *(uint*)&bp[p+4];
                            }
                        }
                        var entry = new EnmityEntry()
                        {
                            ID     = _id,
                            Enmity = _enmity,
                            isMe   = false,
                            Name   = "Unknown",
                            Job    = 0x00
                        };
                        if (entry.ID > 0)
                        {
                            Combatant c = combatantList.Find(x => x.ID == entry.ID);
                            if (c != null)
                            {
                                entry.Name    = c.Name;
                                entry.Job     = c.Job;
                                entry.OwnerID = c.OwnerID;
                            }
                            if (entry.ID == mypc.ID)
                            {
                                entry.isMe = true;
                            }
                            if (TopEnmity == 0)
                            {
                                TopEnmity = entry.Enmity;
                            }
                            entry.HateRate = (int)(((double)entry.Enmity / (double)TopEnmity)*100);
                            enmity.Entries.Add(entry);
                        }
                        else
                        {
                            break; // もう読まない
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Update: {1}", this.Name, ex);
            }
            return serializer.Serialize(enmity);
        }

        private string CreateEventDispatcherScript()
        {
            return "var ActXiv = { 'Enmity': " + this.CreateJsonData() + " };\n" +
                   "document.dispatchEvent(new CustomEvent('onOverlayDataUpdate', { detail: ActXiv }));";
        }

        /// <summary>
        /// スキャン間隔を更新する
        /// </summary>
        public void UpdateScanInterval()
        {
            timer.Interval = this.Config.ScanInterval;
            Log(LogLevel.Debug, Messages.UpdateScanInterval, this.Config.ScanInterval);
        }

        /// <summary>
        /// スキャンを開始する
        /// </summary>
        public new void Start()
        {
            if (OverlayAddonMain.UpdateMessage != String.Empty)
            {
                Log(LogLevel.Info, OverlayAddonMain.UpdateMessage);
                OverlayAddonMain.UpdateMessage = String.Empty;
            }
            if (this.Config.IsVisible == false)
            {
                return;
            }
            timer.Interval = this.Config.ScanInterval;
            timer.Start();
            Log(LogLevel.Info, Messages.StartScanning);
        }

        /// <summary>
        /// スキャンを停止する
        /// </summary>
        public new void Stop()
        {
            if (timer.Enabled)
            {
                timer.Stop();
                Log(LogLevel.Info, Messages.StopScanning);
            }
        }

        protected override void InitializeTimer()
        {
            base.InitializeTimer();
            timer.Interval = this.Config.ScanInterval;
        }

        ///
        /// Job enum
        ///
        public enum JobEnum : byte
        {
            UNKNOWN,
            GLD, // 1
            PGL, // 2
            MRD, // 3
            LNC, // 4
            ARC, // 5
            CNJ, // 6
            THM, // 7
            CRP, // 8
            BSM, // 9
            ARM, // 10
            GSM, // 11
            LTW, // 12
            WVR, // 13
            ALC, // 14
            CUL, // 15
            MIN, // 15
            BTN, // 17
            FSH, // 18
            PLD, // 19
            MNK, // 20
            WAR, // 21
            DRG, // 22
            BRD, // 23
            WHM, // 24
            BLM, // 25
            ACN, // 26
            SMN, // 27
            SCH, // 28
            ROG, // 29
            NIN, // 30
            MCH, // 31
            DRK, // 32
            AST  // 33
        }

        //// 敵視されてるキャラエントリ
        private class EnmityEntry
        {
            public uint ID;
            public uint OwnerID;
            public string Name;
            public uint Enmity;
            public bool isMe;
            public int HateRate;
            public byte Job;
            public string JobName
            {
                get
                {
                    return Enum.GetName(typeof(JobEnum), Job);
                }
            }
            public string EnmityString
            {
                get
                {
                    return Enmity.ToString("##,#");
                }
            }
            public bool isPet
            {
                get
                {
                    return (OwnerID != 0);
                }
            }
        }

        //// JSON用オブジェクト
        private class EnmityObject
        {
            public Combatant Target;
            public List<EnmityEntry> Entries;
        }
    }
}
