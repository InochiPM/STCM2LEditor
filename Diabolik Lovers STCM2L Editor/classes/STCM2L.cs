﻿using STCM2LEditor.classes.Action;
using STCM2LEditor.utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace STCM2LEditor.classes
{
    public class STCM2L
    {
        public string FilePath { get; set; }
        public byte[] OriginalFile { get; set; }
        public List<byte> NewFile { get; set; }
        public int StartPosition { get; set; }
        public byte[] StartData { get; set; }

        public int ExportsPosition { get; set; }
        public int ExportsCount { get; set; }
        public List<Export> Exports { get; set; }

        public int CollectionLinkPosition { get; set; }
        public int CollectionLinkOldAddress { get; set; }
        public IStringAction NewText(IStringAction action, bool before)
        {
            var actionInd = Actions.IndexOf(action);
            var textInd = TextActions.IndexOf(action);
            var textAction = action.Copy();
            if (!before)
            {
                actionInd++;
                textInd++;
            }
            TextActions.Insert(textInd, textAction);
            Actions.Insert(actionInd, textAction);
            return textAction;
        }
        public void DeleteText(IStringAction action)
        {
            TextActions.Remove(action);
            Actions.Remove(action);
        }
        public void DeleteName(IStringAction action)
        {
            foreach (var item in NameActions)
            {
                item.Value.Remove(action);
            }
            Actions.Remove(action);
        }
        public void InsertDivider(IStringAction action)
        {
            Actions.Insert(Actions.IndexOf(action) + 1, new DefaultAction(ActionHelpers.ACTION_DIVIDER, null, 0));
        }
        public void DeleteDivider(IStringAction action)
        {
            Actions.RemoveAt(Actions.IndexOf(action) + 1);
        }
        public Replic NewReplic(Replic replic, bool before)
        {
            int insertInd;

            if (before)
            {
                insertInd = Actions.IndexOf(replic.Lines.First());
            }
            else
            {
                insertInd = Actions.IndexOf(replic.Lines.Last()) + 2;
            }

            IStringAction name = null;
            if (replic.Name is IStringAction nameAction)
            {
                var temp = replic.Name.Copy();
                if (NameActions.TryGetValue(replic.Name.OriginalText, out var list))
                {
                    var ind = Actions.IndexOf(nameAction);
                    if (ind > 0 && before) insertInd = ind;
                    list.Add(temp);
                }
                else
                {
                    list = new BindingList<IStringAction> { temp };
                    NameActions.Add(replic.Name.OriginalText, list);
                }
                Actions.Insert(insertInd, temp);
                insertInd++;
                name = temp;
            }
            else
            {
                string messageBoxCaption = "New page";
                string messageBoxText = "Do you want to create a new page?";
                MessageBoxButton button = MessageBoxButton.YesNo;
                MessageBoxImage image = MessageBoxImage.Question;

                MessageBoxResult result = MessageBox.Show(messageBoxText, messageBoxCaption, button, image);

                if (result == MessageBoxResult.Yes)
                {
                    Actions.Insert(insertInd, new DefaultAction(ActionHelpers.ACTION_NEW_PAGE, null, 0));
                    insertInd++;
                    Actions.Insert(insertInd, new DefaultAction(ActionHelpers.ACTION_DIVIDER, null, 0));
                    insertInd++;
                }
            }
            var text = replic.Lines.First().Copy();
            if (before)
            {
                TextActions.Insert(TextActions.IndexOf(replic.Lines.First()), text);
            }
            else
            {
                TextActions.Insert(TextActions.IndexOf(replic.Lines.Last()) + 1, text);
            }
            Actions.Insert(insertInd, text);
            InsertDivider(text);
            var newReplic = new Replic();
            newReplic.Name = name;
            newReplic.Lines.Add(text);
            return newReplic;
        }
        public void DeleteReplic(Replic replic)
        {
            if (replic.Name is IStringAction)
            {
                var action = replic.Name as IStringAction;
                DeleteName(action);
            }
            DeleteDivider(replic.Lines.Last());
            foreach (var line in replic.Lines)
            {
                DeleteText(line);
            }

        }
        public BindingList<IAction> Actions { get; set; } = new BindingList<IAction>();
        internal Dictionary<string, BindingList<IStringAction>> NameActions { get; set; } = new Dictionary<string, BindingList<IStringAction>>();
        internal BindingList<IStringAction> TextActions { get; set; } = new BindingList<IStringAction>();
        internal Dictionary<string, BindingList<IStringAction>> PlaceActions { get; set; } = new Dictionary<string, BindingList<IStringAction>>();

        public STCM2L(string filePath)
        {
            FilePath = filePath;
            Exports = new List<Export>();
            NewFile = new List<byte>();
        }

        public bool Load()
        {
            Global.ParameterCalls.Clear();
            Global.ActionCalls.Clear();
            OriginalFile = File.ReadAllBytes(FilePath);
            StartPosition = FindStart();
            Exports.Clear();
            NameActions.Clear();
            PlaceActions.Clear();
            TextActions.Clear();
            Actions.Clear();

            if (StartPosition == 0)
            {
                return false;
            }

            ReadStartData();
            ReadCollectionLink();

            ReadExports();

            ReadActions();

            return true;
        }

        public int FileSize()
        {
            int newExportsAddress = StartPosition + GetActionsLength() + 12;
            int newCollectionLinkAddress = newExportsAddress + ExportsCount * STCM2LHelpers.EXPORT_SIZE + 16;
            return newCollectionLinkAddress + 64;
        }
        public bool Save(string filePath)
        {
            NewFile.Clear();

            int newExportsAddress = StartPosition + GetActionsLength() + 12; // + EXPORT_DATA.Length
            int newCollectionLinkAddress = newExportsAddress + ExportsCount * STCM2LHelpers.EXPORT_SIZE + 16; // + COLLECTION_LINK.length
            var extraDataAddress = newCollectionLinkAddress + 64;
            SetAddresses(extraDataAddress);
            StartData = ByteUtil.InsertInt32(StartData, newExportsAddress, STCM2LHelpers.HEADER_OFFSET);
            StartData = ByteUtil.InsertInt32(StartData, newCollectionLinkAddress, STCM2LHelpers.HEADER_OFFSET + 3 * 4);

            NewFile.AddRange(StartData);

            WriteActions();
            WriteExports();
            WriteCollectionLink();

            /*for(int i = 0;i < OriginalFile.Length; i++)
            {
                if (OriginalFile[i] != NewFile[i])
                {
                    //Console.WriteLine("Im't heere");
                }
            }*/

            File.WriteAllBytes(filePath, NewFile.ToArray());
            return true;
        }

        private void WriteActions()
        {

            foreach (var action in Actions)
            {
                NewFile.AddRange(action.Write());
            }
        }

        private void SetAddresses(int extraDataAddress)
        {
            int address = StartPosition;


            foreach (var action in Actions)
            {
                action.Address = address;
                address += action.Length;
            }
            int temp = 0;
            foreach (var item in NameActions)
            {
                foreach (var action in item.Value)
                {
                    address = extraDataAddress;
                    action.SetTranslateAddress(ref address);
                    temp = address;
                }
                extraDataAddress = temp;
            }
            foreach (var item in PlaceActions)
            {
                foreach (var action in item.Value)
                {
                    address = extraDataAddress;
                    action.SetTranslateAddress(ref address);
                    temp = address;
                }
                extraDataAddress = temp;
            }

            foreach (var action in TextActions)
            {
                action.SetTranslateAddress(ref extraDataAddress);
            }
        }

        private void WriteExports()
        {
            NewFile.AddRange(EncodingUtil.encoding.GetBytes("EXPORT_DATA"));
            NewFile.Add(new byte());

            foreach (Export export in Exports)
            {
                NewFile.AddRange(export.Write());
            }
        }

        private void WriteCollectionLink()
        {
            NewFile.AddRange(EncodingUtil.encoding.GetBytes("COLLECTION_LINK"));
            NewFile.Add(new byte());
            NewFile.AddRange(BitConverter.GetBytes(0));

            var extra = new List<byte[]>();
            foreach (var item in NameActions)
            {
                extra.Add(item.Value[0].WriteTranslate());
            }
            foreach (var item in PlaceActions)
            {
                extra.Add(item.Value[0].WriteTranslate());
            }
            foreach (var action in TextActions)
            {
                extra.Add(action.WriteTranslate());
            }

            var sum = (uint)extra.Sum(x => x.Length);

            UInt32 newCollectionLinkAddress = (UInt32)NewFile.Count + 4 + STCM2LHelpers.COLLECTION_LINK_PADDING + sum;

            NewFile.AddRange(BitConverter.GetBytes(newCollectionLinkAddress));
            NewFile.AddRange(new byte[STCM2LHelpers.COLLECTION_LINK_PADDING]);
            foreach (var data in extra)
            {
                NewFile.AddRange(data);
            }
        }

        private int GetActionsLength()
        {
            int length = 0;

            foreach (var action in Actions)
            {
                length += action.Length;
            }

            return length;
        }

        private void ReadStartData()
        {
            int seek = 0;
            StartData = ByteUtil.ReadBytesRef(OriginalFile, StartPosition, ref seek);

            seek = STCM2LHelpers.HEADER_OFFSET;
            ExportsPosition = ByteUtil.ReadInt32Ref(StartData, ref seek);

            seek += 2 * 4;
            CollectionLinkPosition = ByteUtil.ReadInt32Ref(StartData, ref seek);
        }

        private int FindStart()
        {
            byte[] start = EncodingUtil.encoding.GetBytes("CODE_START_");

            for (int i = 0; i < 2000; i++)
            {
                if (OriginalFile[i] == start[0])
                {
                    for (int j = 0; j < start.Length; j++)
                    {
                        if (OriginalFile[i + j] != start[j])
                        {
                            break;
                        }
                        else if (j + 1 == start.Length)
                        {
                            return i + 0x0c;
                        }
                    }
                }
            }

            return 0;
        }

        private void ReadCollectionLink()
        {
            int seek = CollectionLinkPosition + 4;
            CollectionLinkOldAddress = ByteUtil.ReadInt32(OriginalFile, seek);
        }

        private void ReadExports()
        {
            int exportsLength = CollectionLinkPosition - ExportsPosition;
            ExportsCount = exportsLength / STCM2LHelpers.EXPORT_SIZE;

            int seek = ExportsPosition;

            for (int i = 0; i < ExportsCount; i++)
            {
                Export export = new Export();

                seek += 4;

                export.Name = EncodingUtil.encoding.GetString(ByteUtil.ReadBytesRef(OriginalFile, 32, ref seek));
                export.OldAddress = ByteUtil.ReadUInt32Ref(OriginalFile, ref seek);

                Exports.Add(export);
            }
        }
        private void ReadActions()
        {
            int currentAddress = StartPosition;
            int maxAddress = ExportsPosition - 12; // Before EXPORT_DATA
            int currentExport = 0;
            int i = 0;

            do
            {

                var action = DefaultAction.ReadFromFile(OriginalFile, currentAddress, this);
                i++;


                if (currentExport < Exports.Count && Exports[currentExport].OldAddress == currentAddress)
                {
                    Exports[currentExport].ExportedAction = action;
                    currentExport++;
                }

                currentAddress += action.Length;

                Actions.Add(action);
            }
            while (currentAddress < maxAddress);

            Global.RecoverGlobalCalls(Actions);
        }
    }
}
