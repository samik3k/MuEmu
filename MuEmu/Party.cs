﻿using MuEmu.Network.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MuEmu
{
    public class PartyManager
    {
        private static PartyManager _instance;
        private List<Party> _parties;
        public static ushort MaxLevelDiff { get; private set; }

        public static void Initialzie(ushort maxLevelDiff)
        {
            if(_instance == null)
                _instance = new PartyManager();

            MaxLevelDiff = maxLevelDiff;
        }

        public static PartyResults CreateLink(Player master, Player member)
        {
            if(Math.Abs(master.Character.Level - member.Character.Level) > MaxLevelDiff)
            {
                return PartyResults.RestrictedLevel;
            }

            if(member.Character.Party != null)
            {
                return PartyResults.InAnotherParty;
            }

            var party = master.Character.Party;

            if (party == null)
            {
                party = new Party(master, member);
                _instance._parties.Add(party);
                SendAll(party);
                return PartyResults.Success;
            }

            if(party.Count == 5)
            {
                return PartyResults.Fail;
            }

            party.Add(member);
            SendAll(party);
            return PartyResults.Success;
        }

        public static void SendAll(Party party)
        {
            var data = new SPartyList
            {
                Result = PartyResults.Success,
                PartyMembers = party.List(),
            };

            foreach (var memb in party.Members)
                memb.Session.SendAsync(data).Wait();
        }

        public static void Remove(Player plr)
        {
            var party = plr.Character.Party;
            if (party == null)
                return;

            party.Remove(plr);
            if(party.Count == 1)
            {
                party.Close();
                _instance._parties.Remove(party);
                return;
            }

            SendAll(party);
        }
    }

    public class Party
    {
        List<Player> _members;
        ushort _minLevel;
        ushort _maxLevel;

        public Player Master => _members.First();
        public int Count => _members.Count();

        public IEnumerable<Player> Members => _members;

        public Party(Player plr, Player memb)
        {
            _members = new List<Player>
            {
                plr,
                memb,
            };

            plr.Character.Party = this;
            memb.Character.Party = this;
        }

        public bool Any(Player plr)
        {
            return _members.Any(x => x == plr);
        }

        public bool Add(Player plr)
        {
            if (_members.Count == 5)
                return false;

            _members.Add(plr);
            plr.Character.Party = this;
            return true;
        }

        public bool Remove(Player plr)
        {
            if (!Any(plr))
                return false;

            _members.Remove(plr);
            plr.Character.Party = null;
            plr.Session.SendAsync(new SPartyDelUser()).Wait();

            return true;
        }

        public void Close()
        {
            var del = new SPartyDelUser();
            foreach (var memb in Members)
            {
                memb.Session.SendAsync(del).Wait();
                memb.Character.Party = null;
            }

            _members.Clear();
        }

        public Network.Data.PartyDto[] List()
        {
            byte i = 0;
            var data = _members.Select(x => new Network.Data.PartyDto
            {
                Number = i++,
                Id = x.Character.Name,
                Life = (int)x.Character.Health,
                MaxLife = (int)x.Character.MaxHealth,
                Map = x.Character.MapID,
                X = (byte)x.Character.Position.X,
                Y = (byte)x.Character.Position.Y,
            });

            return data.ToArray();
        }
    }
}
