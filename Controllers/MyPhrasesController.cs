using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScaleApiPoc.Data;

namespace scale_api_poc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MyPhrasesController : ControllerBase
{
    private readonly DataContext _context;

    public MyPhrasesController(DataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all phrases from the my_phrases table.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MyPhrase>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var phrases = await _context.MyPhrases
            .OrderBy(p => p.id)
            .ToListAsync(cancellationToken);
        return Ok(phrases);
    }
}
