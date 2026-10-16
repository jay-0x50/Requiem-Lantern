using System;
using System.Linq;

namespace RequiemLantern.Core
{
    // Flavor and action modifiers use no random source and never touch a save.
    public static class CombatSkillFlavor
    {
        public const int MaxBarrier=40, MaxSupportPower=3;
        public static void Configure(UnitState unit)
        {
            ConfigureAwakening(unit);
            foreach(var skill in unit.Skills){
                skill.AnimationKey=unit.Id+"_"+(skill.Number==3?"s3":BattleRules.IsDefensive(skill)?"defense":"s"+skill.Number);
                skill.Quote=Quote(unit.Id,skill.Number,false,0);
            }
            var third=unit.Skills.FirstOrDefault(value=>value.Number==3);
            if(third!=null&&unit.AwakeningMaxStacks>0)third.AwakenedName=unit.AwakeningName+" · "+third.Name;
            if(unit.Id=="lian"&&third!=null){third.CanSupportAlly=true;third.SupportShield=18;third.SupportSanity=2;third.SupportDescription="아군: 보호막 18·정신력 +2 / 각성: 보호막 30·정신력 +4";}
            var second=unit.Skills.FirstOrDefault(value=>value.Number==2);
            if(unit.Id=="kai"&&second!=null){second.CanSupportAlly=true;second.SupportPower=3;second.SupportSanity=2;second.SupportDescription="아군: 다음 공격 행동 전체 위력 +3·정신력 +2";}
            if(unit.Id=="minato"){
                if(second!=null){second.SupportSanity=2;second.SupportDescription="아군 체력 회복·정신력 +2";}
                if(third!=null){third.SupportShield=12;third.SupportSanity=3;third.SupportDescription="아군 체력 회복·보호막 12·정신력 +3";}
            }
        }
        internal static void ConfigureAwakening(UnitState unit)
        {
            unit.AwakeningMaxStacks=0;unit.AwakeningName="";unit.AwakeningDescription="";
            if(unit.IsEnemy)return;
            switch(unit.Id){
                case "lian":unit.AwakeningMaxStacks=3;unit.AwakeningName="꺼지지 않는 파수";unit.AwakeningDescription="방어 행동 실행 또는 맞불 승리마다 +1. 3중첩에서 3스킬 실행 시 3개를 소모해 각성합니다.";break;
                case "sera":unit.AwakeningMaxStacks=3;unit.AwakeningName="잿빛 맹세";unit.AwakeningDescription="공격 행동으로 실제 체력 피해를 주면 +1. 3중첩에서 3스킬 실행 시 3개를 소모해 각성합니다.";break;
                case "noa":unit.AwakeningMaxStacks=3;unit.AwakeningName="되찾은 이름의 서명";unit.AwakeningDescription="맞불 승리마다 +1. 3중첩에서 3스킬 실행 시 3개를 소모해 각성합니다.";break;
            }
        }
        internal static void ResetBattle(UnitState unit)
        { ConfigureAwakening(unit);unit.AwakeningStacks=0;unit.Barrier=0;unit.SupportPower=0; }
        public static SkillData Effective(UnitState actor,SkillData original,LinkKind kind)
        {
            if(actor==null||original==null)return null;
            var skill=original.Copy();skill.Awakened=false;
            if(original.Number==3&&actor.AwakeningMaxStacks>0&&actor.AwakeningStacks>=actor.AwakeningMaxStacks){
                skill.Awakened=true;skill.Name=string.IsNullOrEmpty(original.AwakenedName)?actor.AwakeningName+" · "+original.Name:original.AwakenedName;
                skill.BasePower=BattleRules.Base(original)+4;skill.FlamePower=BattleRules.Flame(original)+1;
                skill.AnimationKey=actor.Id+"_s3_awakened";
                if(actor.Id=="lian"){skill.SupportShield=30;skill.SupportSanity=4;}
            }
            if(kind!=LinkKind.Support&&original.Kind==SkillKind.Attack)skill.BasePower=BattleRules.Base(skill)+Math.Max(0,Math.Min(MaxSupportPower,actor.SupportPower));
            skill.Quote=Quote(actor.Id,skill.Number,skill.Awakened,0);return skill;
        }
        public static string Quote(string id,int number,bool awakened,int variation)
        {
            if(awakened){switch(id){case "lian":return "이번에는, 아무도 두고 가지 않아.";case "sera":return "이 불은, 너희 이름을 잊지 않아!";case "noa":return "내 이름으로, 이 결말을 고치겠어.";}}
            if(number==3){switch(id){case "lian":return "불이 꺼지기 전에, 끝낸다.";case "sera":return "끝까지 타올라!";case "minato":return "돌아갈 숨은, 내가 지킬게.";case "heidel":return "마지막 오차까지, 잡았다.";case "noa":return "네가 지운 기록을, 돌려줄게.";case "kai":return "모두의 빛을, 하나로.";case "mare":return "줄 놓지 마. 같이 돌아간다!";}}
            bool second=(variation&1)==1;
            switch(id){
                case "lian":return number==0?(second?"내 뒤로.":"여긴 내가 막아."):number==2?(second?"길을 비켜.":"방벽을 연다."):(second?"물러서.":"불빛을 따라.");
                case "sera":return number==0?(second?"이쪽이야!":"잡을 수 있겠어?"):number==2?(second?"뜨겁게 간다!":"재로 만들어 줄게!"):(second?"비켜 줄래?":"자, 간다!");
                case "minato":return number==0?(second?"조금만 버텨.":"다치지 않게."):number==2?(second?"숨, 천천히.":"아직 괜찮아."):(second?"잠깐 따끔할 거야.":"손을 빌릴게.");
                case "heidel":return number==0?(second?"각도 수정.":"궤도 읽었다."):number==2?(second?"오차 보정.":"균열, 확인."):(second?"움직임이 커.":"좌표 잡았다.");
                case "noa":return number==0?(second?"그 줄은 틀렸어.":"한 칸 비워 둘게."):number==2?(second?"수정할 차례야.":"이 문장은 지울게."):(second?"여백이 보이네.":"흔적은 남아.");
                case "kai":return number==0?(second?"불빛 안으로.":"곁에 있을게."):number==2?(second?"돌아올 길은 여기야.":"이 빛을 따라와."):(second?"아직 꺼지지 않았어.":"길을 비출게.");
                case "mare":return number==0?(second?"발밑 조심해.":"줄은 내가 잡아."):number==2?(second?"걸렸다, 당겨!":"닻줄, 팽팽히!"):(second?"당길게, 준비해.":"줄 걸었다!");
                default:return "";
            }
        }
    }
}
