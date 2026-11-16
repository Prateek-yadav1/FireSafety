using System;

namespace FireSafety.Extinguishers
{
    public enum ExtinguisherType
    {
        Water,
        CO2,
        DryPowder,
        Foam,
        WetChemical
    }

    [Flags]
    public enum FireClassification
    {
        None = 0,
        ClassA = 1 << 0,       // Combustible solids (wood, paper, textiles)
        ClassB = 1 << 1,       // Flammable liquids (petrol, paint, solvent)
        ClassC = 1 << 2,       // Flammable gases (methane, propane, butane)
        ClassD = 1 << 3,       // Combustible metals (magnesium, sodium)
        Electrical = 1 << 4,   // Energized electrical equipment (appliances, wiring, panels)
        ClassF_K = 1 << 5      // Cooking oils, fats, grease (kitchen fryers, stovetops)
    }
}
