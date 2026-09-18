using UdonSharp;
using VRC.Udon.Common;

namespace Pm.Booth.Aoinu607.Udon.Gateball
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class GateballGameplayControls : UdonSharpBehaviour
    {
        public GateballGameplayState GameplayState;
        public int Mode = GateballGameplayRules.ModePractice;

        public override void Interact()
        {
            _Activate();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (value)
            {
                _Activate();
            }
        }

        public void _Activate()
        {
            if (GameplayState == null)
            {
                return;
            }

            if (GameplayState.GameplayPhase == GateballGameplayRules.PhaseSparkPlacement)
            {
                GameplayState._PlaceSparkDefault();
                return;
            }

            if (Mode == GateballGameplayRules.ModeMatch)
            {
                GameplayState._StartMatch();
            }
            else
            {
                GameplayState._StartPractice();
            }
        }

        public void _StartPractice()
        {
            if (GameplayState != null)
            {
                GameplayState._StartPractice();
            }
        }

        public void _StartMatch()
        {
            if (GameplayState != null)
            {
                GameplayState._StartMatch();
            }
        }

        public void _EndMatch()
        {
            if (GameplayState != null)
            {
                GameplayState._EndMatch();
            }
        }
    }
}
