namespace Brawler.Core
{
    /// <summary>
    /// Anything with a horizontal facing direction. Lets Presentation (e.g. <c>BillboardSprite</c>)
    /// flip sprites without referencing the Player assembly.
    /// </summary>
    public interface IFacingSource
    {
        /// <summary>+1 when facing world +X (right), -1 when facing world -X (left). Never 0.</summary>
        int FacingSign { get; }
    }
}
