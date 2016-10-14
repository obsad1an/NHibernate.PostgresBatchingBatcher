# NHibernate.PostgresBatchingBatcher (Beta release. Use at own risk.)

After finding out that NHibernate doesn't have an implementation for [batching in Postgres](https://github.com/nhibernate/nhibernate-core/tree/master/src/NHibernate/AdoNet), I decided to make this library based on this old Stackoverflow [post](http://stackoverflow.com/questions/4611337/nhibernate-does-not-seems-doing-bulk-inserting-into-postgresql)

Credits to [Gerrit](http://stackoverflow.com/users/960796/gerrit) for the initial code.

## Important

This code has been tested on a production enviroment for a specific project. If well it does work and you will notice a big performance improvement, you have to actually know what for INSERT and UPDATE statements you send to the batcher.
By INSERT you wouln't find any problem, but if you send an UPDATE with more WHEREs as the conditioning UPDATE, you might run into a problem (BE CAREFUL).
Have in mind that the [ADO.NET Data Provider for PostgreSQL](http://www.npgsql.org) doesn't count with a SQL Parser [READ HERE](https://github.com/npgsql/npgsql/issues/1042). This is the reason why NHibernate Core does not implement a Postgres Batcher.

### Example UPDATE with more than one WHERE in statement:

	UPDATE table_name
	SET column_name = (SELECT expression_1
		       FROM table_2
		       WHERE conditions)
	WHERE conditions;

## Usage
	
	config.DataBaseIntegration(db =>
		{
			db.BatchSize = 500; //this batch size is an example, set as needed
			db.Batcher<PostgresBatchingBatcherFactory>();
		});


## What does work:

	- Inserts batching
	- Updates batching only when the where statement only has one conditional. Which is the most common case when NHibernate sends huge update amount statements.

## Work to do:

	- Change the way inserts are being batched. (many batched inserts instead of one huge insert (test if this avoids problems with too many statements))
	- Code Refactoring
	- Build an actual parser?
	- Unit Testing

## Updates

### 10/10/2016
	Update batching works for most cases (Read the "Important" statement)
	Helper methods moved

### 21/09/2016
	Update batching functionality added. Needs to be extended.
 
### 19/09/2016
	Initial commit. Code moved to a solution. Only insert batching worked.
	
	
## Reference documentation:

[Postgres Keyords](https://www.postgresql.org/docs/7.3/static/sql-keywords-appendix.html)

[Update Statement](https://www.postgresql.org/docs/9.3/static/sql-update.html)
