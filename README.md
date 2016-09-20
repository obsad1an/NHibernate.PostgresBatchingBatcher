# NHibernate.PostgresBatcher

After finding out that NHibernate doesn't have an implementation for [batching in Postgres](https://github.com/nhibernate/nhibernate-core/tree/master/src/NHibernate/AdoNet), I decided to make this library based on this old [post])(http://stackoverflow.com/questions/4611337/nhibernate-does-not-seems-doing-bulk-inserting-into-postgresql) on Stackoverflow

This means credits to [Gerrit](http://stackoverflow.com/users/960796/gerrit)

The code needs a lot of refactoring which I intend to do. Feel free to contribute.

So far it works batching inserts and I am currently working on batching updates.

## Usage
	
	config.DataBaseIntegration(db =>
		{
			db.BatchSize = 500;
			db.Batcher<PostgresBatcherFactory>();
		});