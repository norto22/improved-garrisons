using System.Linq;
using ImprovedGarrisons.AI.AIManagers;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ImprovedGarrisons.ImprovedGarrisonsUI.UIElements
{
    public class SurplusGuardReadinessVM : ViewModel
    {
        public string Text { get; private set; }

        public void Refresh(TroopRoster roster, GarrisonSettings settings, bool hasGuard, bool underAttack)
        {
            TextObject message;
            if (settings == null)
            {
                message = new TextObject("{=ui_guardsui_surplus_select}Surplus guards: Select a settlement");
            }
            else if (!settings.GuardsAutoSpawnFromExcess)
            {
                message = new TextObject("{=ui_guardsui_surplus_off}Surplus guards: off");
            }
            else if (settings.Template == null || settings.Template.AmountOfTroopsInTemplate == 0)
            {
                message = new TextObject("{=ui_guardsui_surplus_template}Surplus guards: Set a template");
            }
            else if (settings.GuardsAutoSpawnSize <= 0)
            {
                message = new TextObject("{=ui_guardsui_surplus_size}Surplus guards: Set a guard party size");
            }
            else if (roster == null)
            {
                message = new TextObject("{=ui_guardsui_surplus_garrison}Surplus guards: No garrison");
            }
            else
            {
                int eligible = TemplateSurplusTroops.GetSurplusTroops(roster, settings.Template.GetTroopListAsCharacterObjects()).Values.Sum();
                TextObject reason;
                if (underAttack)
                {
                    reason = new TextObject("{=ui_guardsui_surplus_attack}Settlement under attack");
                }
                else if (hasGuard)
                {
                    reason = new TextObject("{=ui_guardsui_surplus_active}Guard already active");
                }
                else if (eligible < settings.GuardsAutoSpawnSize)
                {
                    reason = new TextObject("{=ui_guardsui_surplus_waiting}Waiting for troops");
                }
                else
                {
                    reason = new TextObject("{=ui_guardsui_surplus_ready}Ready on next hourly check");
                }
                message = new TextObject("{=ui_guardsui_surplus_readiness}Surplus guards: {ELIGIBLE} / {REQUIRED} eligible troops · {STATUS}");
                message.SetTextVariable("ELIGIBLE", eligible);
                message.SetTextVariable("REQUIRED", settings.GuardsAutoSpawnSize);
                message.SetTextVariable("STATUS", reason);
            }

            string value = message.ToString();
            if (Text != value)
            {
                Text = value;
                OnPropertyChangedWithValue(value, nameof(Text));
            }
        }
    }
}
