﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using TeleporterPlugin.Managers;
using TeleporterPlugin.Objects;

//TODO BUG Fix Estate TP if not on Home World

namespace TeleporterPlugin.Gui {
    public class ConfigurationWindow : Window<TeleporterPlugin> {
        private static readonly Vector4 ColorRed = new Vector4(255, 0, 0, 255);
        private readonly string[] _languageList;
        private int _selectedLanguage;
        private DateTime _lastAetheryteListUpdate = DateTime.MinValue;
        private string[] _aetheryteList = new string[0];
        private string[] _mapList = new string[0];

        public Configuration Config => Plugin.Config;

        public ConfigurationWindow(TeleporterPlugin plugin) : base(plugin) {
            _languageList = Enum.GetNames(typeof(TeleporterLanguage));
            _selectedLanguage = (int)plugin.Config.TeleporterLanguage;
        }

        protected override void DrawUi() {
            ImGui.SetNextWindowSize(new Vector2(530, 450), ImGuiCond.Appearing);
            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 300), new Vector2(float.MaxValue, float.MaxValue));
            if (!ImGui.Begin($"{Plugin.Name} Config", ref WindowVisible, ImGuiWindowFlags.NoScrollWithMouse)) {
                ImGui.End();
                return;
            }

            if (ImGui.BeginChild("##SettingsRegion")) {
                if (ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen))
                    DrawGeneralSettings();

                ImGui.Spacing();
                if (ImGui.CollapsingHeader("Alias Settings", ImGuiTreeNodeFlags.DefaultOpen))
                    DrawAliasSettings();

                ImGui.EndChild();
            }

            ImGui.End();
        }

        private void UpdateAetheryteList() {
            if (DateTime.UtcNow.Subtract(_lastAetheryteListUpdate).TotalMilliseconds < 5000)
                return;
            var list = Plugin.Manager.AetheryteList.ToList();
            var mapList = new HashSet<string>();
            foreach (var location in list) {
                var locF = AetheryteDataManager.GetAetheryteLocationsByTerritoryId(location.ZoneId, Plugin.Language).FirstOrDefault();
                if(locF == null) continue;
                mapList.Add(locF.TerritoryName);
            }
            _mapList = mapList.ToArray();
            _aetheryteList = list.Select(a => a.Name).ToArray();
            _lastAetheryteListUpdate = DateTime.UtcNow;
        }

        private void DrawGeneralSettings() {
            if (ImGui.Checkbox("Allow partial Name matching", ref Config.AllowPartialMatch)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Matches the first Aetheryte found that contains ...\n" +
                                 "e.g.: 'kug' matches 'Kugane' or 'gridania' matches 'New Gridania'");
            ImGui.SameLine();
            if (ImGui.Checkbox("Partial Alias", ref Config.AllowPartialAlias)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Match the start of Alias names");

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextUnformatted("Tooltips");
            ImGui.AlignTextToFramePadding();
            ImGui.SameLine();
            if (ImGui.Checkbox("##hideTooltipsOnOff", ref Config.ShowTooltips)) Config.Save();

            ImGui.TextUnformatted("Language:");
            if (Plugin.Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Change the Language used for Aetheryte Names.\n" +
                                 $"(default) Client = Game Language [{Plugin.Interface.ClientState.ClientLanguage}]");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##hideLangSetting", ref _selectedLanguage, _languageList, _languageList.Length)) {
                Config.TeleporterLanguage = (TeleporterLanguage)_selectedLanguage;
                Config.Save();
                _lastAetheryteListUpdate = DateTime.MinValue;
            }

            if (ImGui.Checkbox("Skip Ticket Popup", ref Config.SkipTicketPopup)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Removes the 'Use an aetheryte ticket to teleport...' popup when using Teleporter commands");

            if (ImGui.Checkbox("Use Tickets if Gil Price is above:", ref Config.UseGilThreshold)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Attempt to use Aetheryte Tickets if the Teleport cost is greater than this value.\n" +
                                 "Applies to '/tp'.\n" +
                                 "e.g.: '/tp Kugane' will attempt to use a ticket if the price is above the set value as if '/tpt Kugane' was used");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputInt("##hideInputGil", ref Config.GilThreshold, 1, 10)) {
                if (Config.GilThreshold < 0) Config.GilThreshold = 0;
                if (Config.GilThreshold > 999_999_999) Config.GilThreshold = 999_999_999;
                Config.Save();
            }

            if (ImGui.Checkbox("Enable AetherGate", ref Config.UseFloatingWindow))
                Config.Save();
            if (Plugin.Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("Show a Window with customizable Buttons to quickly Teleport around.\n" +
                                 "Rightclick on the Window or click the + Button to add a new Button\n" +
                                 "Rightlick on any Button to Edit or Delete it");
            if(ImGui.Checkbox("Show Teleporter Messages in Chat", ref Config.PrintMessage))
                Config.Save();
        }

        private void DrawAliasSettings() {
            var newAliasAdded = false;
            if (ImGui.Button("New Alias")) {
                Config.AliasList.Insert(0, TeleportAlias.Empty);
                newAliasAdded = true;
            }

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            if (ImGui.Button("Delete")) {
                if (Config.AliasList.Count > 0)
                    Config.AliasList.RemoveAt(0);
                Config.Save();
            }

            var deleteAliasHovered = ImGui.IsItemHovered();

            ImGui.SameLine();
            if (ImGui.Button("Delete Selected")) {
                Config.AliasList.RemoveAll(a => a.GuiSelected);
                Config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Delete All"))
                ImGui.OpenPopupOnItemClick("deleteallpopup", 0);

            if (ImGui.BeginPopup("deleteallpopup")) {
                ImGui.TextColored(ColorRed, "Are you sure you want to delete ALL aliases?");
                if (ImGui.Button("No", new Vector2(80, ImGui.GetTextLineHeightWithSpacing())))
                    ImGui.CloseCurrentPopup();
                ImGui.SameLine(ImGui.GetWindowWidth() - 90);
                if (ImGui.Button("Yes", new Vector2(80, ImGui.GetTextLineHeightWithSpacing()))) {
                    Config.AliasList.Clear();
                    Config.Save();
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.Separator();
            if (!ImGui.BeginChild("##scrollingregionAlias", Vector2.Zero))
                return;
            if (newAliasAdded) ImGui.SetScrollHereY();
            ImGui.Columns(2);
            ImGui.TextUnformatted("Alias");
            ImGui.NextColumn();
            ImGui.TextUnformatted("Target Aetheryte");
            ImGui.NextColumn();
            ImGui.Separator();

            UpdateAetheryteList();
            for (var i = 0; i < Config.AliasList.Count; i++) {
                var alias = Config.AliasList[i];
                if (deleteAliasHovered && i == 0) ImGui.ArrowButton("delete_indicator", ImGuiDir.Right);
                else ImGui.Checkbox($"##hidelabelAliasSelected{i}", ref alias.GuiSelected);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetColumnWidth() - 45);
                if (deleteAliasHovered && i == 0) ImGui.TextColored(ColorRed, alias.Alias);
                else if (ImGui.InputText($"##hidelabelAliasKey{i}", ref alias.Alias, 256))
                    Config.Save();
                ImGui.NextColumn();
                if (deleteAliasHovered && i == 0) {
                    ImGui.TextColored(ColorRed, alias.Aetheryte);
                } else {
                    ImGui.SetNextItemWidth(ImGui.GetColumnWidth() - 75);
                    if (ImGui.InputText($"##hidelabelAliasValue{i}", ref alias.Aetheryte, 256))
                        Config.Save();
                    ImGui.SameLine();
                    if (ImGui.BeginCombo($"##hidelabelAliasSelect{i}", "", ImGuiComboFlags.NoPreview)) {
                        for (var a = 0; a < _aetheryteList.Length; a++) {
                            var selected = alias.Aetheryte.Equals(_aetheryteList[a], StringComparison.OrdinalIgnoreCase);
                            if (ImGui.Selectable(_aetheryteList[a], selected)) {
                                alias.Aetheryte = _aetheryteList[a];
                                Config.Save();
                            }
                            if (selected) ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                    if(ImGui.IsItemHovered())
                        ImGui.SetTooltip("Aetheryte Names");
                    ImGui.SameLine();
                    if (ImGui.BeginCombo($"##hidelabelAliasSelectMap{i}", "", ImGuiComboFlags.NoPreview)) {
                        for (var a = 0; a < _mapList.Length; a++) {
                            var selected = alias.Aetheryte.Equals(_mapList[a], StringComparison.OrdinalIgnoreCase);
                            if (ImGui.Selectable(_mapList[a], selected)) {
                                alias.Aetheryte = _mapList[a];
                                Config.Save();
                            }
                            if (selected) ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Map Names");
                }

                ImGui.NextColumn();
            }

            ImGui.EndChild();
            ImGui.Columns(1);
        }
    }
}