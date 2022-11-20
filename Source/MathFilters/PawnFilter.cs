﻿using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace CrunchyDuck.Math.MathFilters {
	class PawnFilter : MathFilter {
		Dictionary<string, Pawn> contains = new Dictionary<string, Pawn>();
		private bool primedForTrait = false;
		private bool canCount = true;
		public override bool CanCount { get { return canCount; } }

		public static Dictionary<string, Func<Pawn, bool>> filterMethods = new Dictionary<string, Func<Pawn, bool>>() {
			{ "pawns", p => !p.AnimalOrWildMan() },
			
			{ "colonists", p => IsColonist(p) },
			{ "col", p => IsColonist(p) },

			{ "mechanitors", p => p.mechanitor != null },
			{ "mech", p => p.mechanitor != null },

			{ "prisoners", p => p.IsPrisonerOfColony },
			{ "pri", p => p.IsPrisonerOfColony },

			{ "slaves", p => p.IsSlaveOfColony },
			{ "slv", p => p.IsSlaveOfColony },

			{ "guests", p => IsGuest(p) },

			{ "animals", p => p.AnimalOrWildMan()},
			{ "anim", p => p.AnimalOrWildMan()},

			{ "kids", p => p.DevelopmentalStage == DevelopmentalStage.Child},

			{ "babies", p => p.DevelopmentalStage == DevelopmentalStage.Baby || p.DevelopmentalStage == DevelopmentalStage.Newborn},
			{ "bab", p => p.DevelopmentalStage == DevelopmentalStage.Baby || p.DevelopmentalStage == DevelopmentalStage.Newborn},

			{ "male", IsMalePawn },
			{ "female", IsFemalePawn },
		};

		public static Dictionary<string, Func<Pawn, float>> counterMethods = new Dictionary<string, Func<Pawn, float>>() {
			{ "bandwidth", GetBandwidth },
			{ "intake", GetIntake },
		};

		public PawnFilter(BillComponent bc) {
			// Pawns who share the same name will only be counted once.
			//This shouldn't be a problem for most people,
			// and right now I'm not mentioning it anywhere so people don't complain about it.
			contains = bc.Cache.pawns_dict;
		}

		public override float Count() {
			return contains.Count;
		}

		public override ReturnType Parse(string command, out object result) {
			result = null;
			// We were expecting a trait.
			if (primedForTrait) {
				if (!Math.searchableTraits.ContainsKey(command)) {
					return ReturnType.Null;
				}
				primedForTrait = false;
				canCount = true;

				Dictionary<string, Pawn> filtered_pawns = new Dictionary<string, Pawn>();
				foreach (KeyValuePair<string, Pawn> entry in contains) {
					if (HasTrait(entry.Value, command)) {
						filtered_pawns[entry.Key] = entry.Value;
					}
				}
				contains = filtered_pawns;
				result = this;
				return ReturnType.PawnFilter;
			}
			if (command == "traits") {
				primedForTrait = true;
				result = this;
				canCount = false;
				return ReturnType.PawnFilter;
			}


			// Search pawn.
			if (contains.ContainsKey(command)) {
				contains = new Dictionary<string, Pawn>() {
					{ command, contains[command] }
				};
				result = this;
				return ReturnType.PawnFilter;
			}
			// Search filter.
			if (filterMethods.ContainsKey(command)) {
				var method = filterMethods[command];
				Dictionary<string, Pawn> filtered_pawns = new Dictionary<string, Pawn>();
				foreach(KeyValuePair<string, Pawn> entry in contains) {
					if (method.Invoke(entry.Value)) {
						filtered_pawns[entry.Key] = entry.Value;
					}
				}
				contains = filtered_pawns;
				result = this;
				return ReturnType.PawnFilter;
			}
			// Search counter.
			if (counterMethods.ContainsKey(command)) {
				var method = counterMethods[command];
				float count = 0;
				foreach (Pawn p in contains.Values) {
					count += method.Invoke(p);
				}
				result = count;
				return ReturnType.Count;
			}

			return ReturnType.Null;
		}

		//public override ReturnType ParseType(string command) {
		//	if (primedForTrait) {
		//		if (!Math.searchableTraits.ContainsKey(command)) {
		//			return ReturnType.Null;
		//		}
		//		return ReturnType.Count;
		//	}



		//	// If we can't find anything else, they have to be attempting to search for a pawn or something invalid.
		//	return ReturnType.PawnFilter;
		//}

		// Filters
		private static bool HasTrait(Pawn p, string trait_name) {
			var (traitDef, index) = Math.searchableTraits[trait_name];
			var trait_degree = traitDef.degreeDatas[index].degree;
			if (p.story.traits.HasTrait(traitDef, trait_degree))
				return true;
			return false;
		}

		private static bool IsMalePawn(Pawn pawn) {
			return pawn.gender == Gender.Male;
		}

		private static bool IsFemalePawn(Pawn pawn) {
			return pawn.gender == Gender.Female;
		}

		/// <summary>
		/// The base game defines a colonist as anyone who appears in the colonist bar at the top of the screen. This includes slaves and quest loders.
		/// My definition does not include them.
		/// </summary>
		private static bool IsColonist(Pawn p) {
			return !p.AnimalOrWildMan() && !p.IsPrisoner && !p.IsSlave && !IsGuest(p);
		}

		/// <summary>
		/// Guests include quest lodgers (from Royalty) and visitors (from Hospitality)
		/// </summary>
		private static bool IsGuest(Pawn p) {
			// Hospitality doesn't seem to use p.GuestStatus.
			// Game also considers slaves and prisoners as guests. lol.
			return (!p.IsSlave && !p.IsPrisoner) && (p.IsQuestLodger() || p.guest?.HostFaction == Faction.OfPlayer);
		}

		// Counters
		private static float GetIntake(Pawn pawn) {
			float intake = 0;
			try {
				// This whole thing feels absurd, but I don't know how else I'm meant to get the hunger rate.
				// I searched everywhere but it does seem like the stats menu is the only location it's displayed with all modifiers.
#if v1_3
			Match match = v13_getIntake.Match(RaceProperties.NutritionEatenPerDayExplanation(p, showCalculations: true));
			if (match.Success) {
				intake += float.Parse(match.Groups[1].Value);
			}
			//intake += float.Parse(RaceProperties.NutritionEatenPerDayExplanation(p, showCalculations: true));
#elif v1_4
				// See: Need_Food.BaseHungerRate
				intake += float.Parse(RaceProperties.NutritionEatenPerDay(pawn));
#endif
			}
			// This occurs if a pawn dies. Goes away on its own eventually.
			catch (NullReferenceException) {
				return 0;
			}
			return intake;
		}

		private static float GetBandwidth(Pawn pawn) {
			var mechanitor = pawn.mechanitor;
			if (mechanitor != null)
				return mechanitor.TotalBandwidth - mechanitor.UsedBandwidth;
			return 0;
		}

	}
}
