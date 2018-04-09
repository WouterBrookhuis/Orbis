﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Orbis.World;

/// <summary>
/// Author: Bram Kelder, Wouter Brookhuis, Kaj van der Veen
/// </summary>
namespace Orbis.Simulation
{
    public class Civilization
    {
        #region Constants
        // Default Base modifiers
        private const double DEFAULT_BASE_EXPAND = 1;
        private const double DEFAULT_BASE_EXTERMINATE = 1;

        // Used for war decisions.
        private const int HATE_THRESHOLD = -100;       // Minimum dislike for starting wars.
        private const float HATE_MOD = 0.1F;           // Modifier applied to hate for decisions.
        private const float WAR_COOLDOWN_MOD = 2F;     // Modifier applied to war cooldown for decisions.
        private const int WAR_COOLDOWN_VALUE = 20;     // Value added to war cooldown after a war ends.
        private const int WAR_COOLDOWN_DECAY = 1;      // Amount by which the war cooldown decays each month.
        #endregion

        /// <summary>
        /// The name of the civ
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// If the civ has died
        /// </summary>
        public bool IsAlive
        {
            get => _isAlive;
            set => _isAlive = value;
        }

        private bool _isAlive;
        /// <summary>
        /// Is currently at war
        /// </summary>
        public bool AtWar { get => _currentWars.Count != 0; }

        /// <summary>
        /// The number of wars this civilization is involved in.
        /// </summary>
        public int WarCount { get => _currentWars.Count; }
        private List<War> _currentWars;
        private int _warCooldown;

        /// <summary>
        /// The cells owned by this civ
        /// </summary>
        public HashSet<Cell> Territory { get; set; }

        /// <summary>
        /// All neighbour cells of the civs territory
        /// </summary>
        public HashSet<Cell> Neighbours { get; set; }

        /// <summary>
        ///     The opinion this civ has of neighbouring civs.
        /// </summary>
        public Dictionary<Civilization, int> CivOpinions { get; set; }

        /// <summary>
        /// The total population of the civ
        /// </summary>
        public int Population
        {
            get
            {
                return _population;
            }
            set
            {
                _population = value;
                if (_population <= 0)
                {
                    _isAlive = false;
                }
            }
        }
        private int _population;

        public Color Color { get; set; }

        public int TotalHousing { get; set; }
        public double TotalWealth { get; set; }
        public double TotalResource { get; set; }

        public double BaseExpand = DEFAULT_BASE_EXPAND;
        public double BaseExterminate = DEFAULT_BASE_EXTERMINATE;

        public Civilization()
        {
            _isAlive = true;
            Territory = new HashSet<Cell>();
            Neighbours = new HashSet<Cell>();
            _currentWars = new List<War>();
            CivOpinions = new Dictionary<Civilization, int>();
            _warCooldown = 0;
        }

        /// <summary>
        /// Determane what action to perform next
        /// </summary>
        /// <returns></returns>
        public SimulationAction DetermineAction()
        {
            // Start by trimming dead neighbours from the opinions.
            int neighbourCivs = CivOpinions.Count;
            for (int i = 0; i < neighbourCivs; i++)
            {
                Civilization neighbour = CivOpinions.ElementAt(i).Key;
                if (!neighbour.IsAlive)
                {
                    CivOpinions.Remove(neighbour);
                    i--;
                    neighbourCivs--;
                }
            }

            SimulationAction action = new SimulationAction(this, CivDecision.DONOTHING, null);

            _warCooldown = MathHelper.Clamp(_warCooldown - WAR_COOLDOWN_DECAY, 0, 100);

            if (AtWar)
            {
                return null;
            }

            double expand = 1, exterminate = 1;

            expand *= BaseExpand + (Population / (double)TotalHousing);

            // Find the most suitable war target.
            KeyValuePair<Civilization, int> warTarget = CivOpinions.OrderBy(c => c.Value).FirstOrDefault(c => c.Value < HATE_THRESHOLD);
            exterminate *= BaseExterminate;
            exterminate -= _warCooldown * WAR_COOLDOWN_MOD;
            exterminate = (warTarget.Key != null) ? exterminate + MathHelper.Clamp(Math.Abs(warTarget.Value) * HATE_MOD, 0, 10): 0;

            if (expand > exterminate)
            {
                if (Neighbours.Count <= 0)
                {
                    LoseCell(Territory.FirstOrDefault());
                    return null;
                }

                Cell cell = Neighbours.First();

                int cellCount = Neighbours.Count;
                foreach (Cell c in Neighbours)
                {
                    if (CalculateCellValue(c) > CalculateCellValue(cell))
                    {
                        cell = c;
                    }
                }

                action.Action = CivDecision.EXPAND;
                action.Params = new object[] { cell };
            }
            else
            {
                action.Action = CivDecision.EXTERMINATE;
                action.Params = new object[] { warTarget.Key };
            }

            return (action.Action != CivDecision.DONOTHING) ? action : null;
        }

        public double CalculateCellValue(Cell cell)
        {
            // Calculate value based on needs.
            double val = (cell.MaxHousing / 1000d) + (cell.FoodMod) + (cell.ResourceMod) + (cell.WealthMod);

            if (cell.Owner == null)
            {
                val += 2.5;
            }
            else if (cell.Owner != null)
            {
                val *= -10 + BaseExterminate;
            }

            // Add value for each neighbour cell.
            int cellCount = cell.Neighbours.Count;
            for (int i = 0; i < cellCount; i++)
            {
                if (cell.Neighbours[i].Owner == this)
                {
                    val += 2.5;
                }
            }

            return val;
        }

        public bool LoseCell(Cell cell)
        {
            if (cell == null || cell.Owner != this)
            {
                return false;
            }

            if (!Territory.Remove(cell))
            {
                return false;
            }

            cell.Owner = null;
            HashSet<Cell> newNeighbours = new HashSet<Cell>();

            for (var neighbourIndex = 0; neighbourIndex < cell.Neighbours.Count; neighbourIndex++)
            {
                Cell c = cell.Neighbours[neighbourIndex];
                Neighbours.Remove(c);

                if (c.Owner != this) continue;

                for (var newNeighbourIndex = 0; newNeighbourIndex < c.Neighbours.Count; newNeighbourIndex++)
                {
                    Cell cc = c.Neighbours[newNeighbourIndex];
                    if (cc.Owner != this)
                    {
                        newNeighbours.Add(cc);
                    }
                }
            }

            foreach(Cell c in newNeighbours)
            {
                Neighbours.Add(c);
            }

            return true;
        }

        /// <summary>
        /// Add a cell to this civs territory
        /// </summary>
        /// <param name="cell">Cell to add</param>
        /// <returns>True if succesfull</returns>
        public bool ClaimCell(Cell cell)
        {
            cell.Owner?.LoseCell(cell);

            cell.Owner = this;
            Territory.Add(cell);

            for (var neighbourIndex = 0; neighbourIndex < cell.Neighbours.Count; neighbourIndex++)
            {
                Cell c = cell.Neighbours[neighbourIndex];
                if (c.Owner == this) continue;

                Neighbours.Add(c);

                if (c.Owner == null) continue;

                if (!CivOpinions.ContainsKey(c.Owner))
                {
                    CivOpinions.Add(c.Owner, -20);
                }
                else
                {
                    CivOpinions[c.Owner] -= 20;
                }
            }

            Neighbours.Remove(cell);
            TotalHousing += cell.MaxHousing;

            cell.population = (!cell.IsWater) ? 100 : 0;

            return true;
        }

        public void StartWar(War war)
        {
            _currentWars.Add(war);
        }

        public void EndWar(War war)
        {
            _currentWars.Remove(war);
            _warCooldown = MathHelper.Clamp(_warCooldown + WAR_COOLDOWN_VALUE, 0, 100);
        }
    }
}
