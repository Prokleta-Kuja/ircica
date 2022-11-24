namespace ircica.QueryParams;

public class Params
{
    public Params(string orderBy, bool descending = false)
    {
        OrderBy = orderBy;
        OrderDesc = descending;
    }
    public string OrderBy { get; private set; }
    public bool OrderDesc { get; private set; }
    public string? SearchTerm { get; private set; }
    public bool Executing { get; set; }
    public int Skip { get; set; }
    public Params SetOrderBy(string orderBy)
    {
        if (OrderBy == orderBy)
            OrderDesc = !OrderDesc;
        else
            OrderDesc = false;

        OrderBy = orderBy;

        return this;
    }
    public Params SetSearchTerm(string term)
    {
        SearchTerm = term;
        return this;
    }
    public Params ClearSearchTerm()
    {
        SearchTerm = null;
        return this;
    }
}