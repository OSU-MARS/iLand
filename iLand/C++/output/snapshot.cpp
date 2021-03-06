/********************************************************************************************
**    iLand - an individual based forest landscape and disturbance model
**    http://iland.boku.ac.at
**    Copyright (C) 2009-  Werner Rammer, Rupert Seidl
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU General Public License as published by
**    the Free Software Foundation, either version 3 of the License, or
**    (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU General Public License for more details.
**
**    You should have received a copy of the GNU General Public License
**    along with this program.  If not, see <http://www.gnu.org/licenses/>.
********************************************************************************************/
#include "snapshot.h"
#include "global.h"
#include "globalsettings.h"
#include "model.h"
#include "resourceunit.h"
#include "species.h"
#include "tree.h"
#include "soil.h"
#include "snag.h"
#include "saplings.h"
#include "debugtimer.h"
#include "watercycle.h"
#include "expressionwrapper.h"
#include "helper.h"
#include "gisgrid.h"
#include "mapgrid.h"

#include <QString>
#include <QtSql>
//#include <QVector3D>


Snapshot::Snapshot()
{
}

bool Snapshot::openDatabase(const QString &file_name, const bool read)
{
    if (!GlobalSettings::instance()->setupDatabaseConnection("snapshot", file_name, read)) {
        throw IException("Snapshot:createDatabase: database could not be created / opened");
    }
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    if (!read) {
        // create tables
        QSqlQuery q(db);
        // trees
        q.exec("drop table trees");
        q.exec("create table trees (ID integer, RUindex integer, posX integer, posY integer, species text,  age integer, height real, dbh real, leafArea real, opacity real, foliageMass real, woodyMass real, fineRootMass real, coarseRootMass real, NPPReserve real, stressIndex real)");
        // soil
        q.exec("drop table soil");
        q.exec("create table soil (RUindex integer, kyl real, kyr real, inLabC real, inLabN real, inLabP real, inRefC real, inRefN real, inRefP real, YLC real, YLN real, YLP real, YRC real, YRN real, YRP real, SOMC real, SOMN real, WaterContent, SnowPack real)");
        // snag
        q.exec("drop table snag");
        q.exec("create table snag(RUIndex integer, climateFactor real, SWD1C real, SWD1N real, SWD2C real, SWD2N real, SWD3C real, SWD3N real, " \
               "totalSWDC real, totalSWDN real, NSnags1 real, NSnags2 real, NSnags3 real, dbh1 real, dbh2 real, dbh3 real, height1 real, height2 real, height3 real, " \
               "volume1 real, volume2 real, volume3 real, tsd1 real, tsd2 real, tsd3 real, ksw1 real, ksw2 real, ksw3 real, halflife1 real, halflife2 real, halflife3 real, " \
               "branch1C real, branch1N real, branch2C real, branch2N real, branch3C real, branch3N real, branch4C real, branch4N real, branch5C real, branch5N real, branchIndex integer)");
        // saplings/regeneration
        q.exec("drop table saplings");
        q.exec("create table saplings (RUindex integer, species text, posx integer, posy integer, age integer, height float, stress_years integer)");
        qDebug() << "Snapshot - tables created. Database" << file_name;
    }
    return true;
}

bool Snapshot::openStandDatabase(const QString &file_name, bool read)
{
    if (!GlobalSettings::instance()->setupDatabaseConnection("snapshotstand", file_name, read)) {
        throw IException("Snapshot:createDatabase: database could not be created / opened");
    }
    return true;

}

bool Snapshot::createSnapshot(const QString &file_name)
{
    openDatabase(file_name, false);
    // save the trees
    saveTrees();
    // save soil pools
    saveSoil();
    // save snags / deadwood pools
    saveSnags();
    // save saplings
    saveSaplings();
    QSqlDatabase::database("snapshot").close();
    // save a grid of the indices
    QFileInfo fi(file_name);
    QString grid_file = fi.absolutePath() + "/" + fi.completeBaseName() + ".asc";
    Grid<double> index_grid;
    index_grid.setup( GlobalSettings::instance()->model()->RUgrid().metricRect(), GlobalSettings::instance()->model()->RUgrid().cellsize());
    RUWrapper ru_wrap;
    Expression ru_value("index", &ru_wrap);
    double *grid_ptr = index_grid.begin();
    for (ResourceUnit **ru = GlobalSettings::instance()->model()->RUgrid().begin(); ru!=GlobalSettings::instance()->model()->RUgrid().end(); ++ru, ++grid_ptr) {
        if (*ru) {
            ru_wrap.setResourceUnit(*ru);
            *grid_ptr = ru_value.execute();
        } else
            *grid_ptr = -1.;
    }
    QString grid_text = gridToESRIRaster(index_grid);
    Helper::saveToTextFile(grid_file, grid_text);
    qDebug() << "saved grid to " << grid_file;

    return true;
}

bool Snapshot::loadSnapshot(const QString &file_name)
{
    DebugTimer t("loadSnapshot");
    openDatabase(file_name, true);


    QFileInfo fi(file_name);
    QString grid_file = fi.absolutePath() + "/" + fi.completeBaseName() + ".asc";
    GisGrid grid;
    mRUHash.clear();

    if (!grid.loadFromFile(grid_file)) {
        qDebug() << "loading of snapshot: not a valid grid file (containing resource unit inidices) expected at:" << grid_file;
        for (ResourceUnit **ru = GlobalSettings::instance()->model()->RUgrid().begin(); ru!=GlobalSettings::instance()->model()->RUgrid().end();++ru) {
            if (*ru)
                mRUHash[ (*ru)->index() ] = *ru;
        }
    } else {
        // setup link between resource unit index and index grid:
        // store for each resource unit *in the snapshot database* the corresponding
        // resource unit index of the *current* simulation.
        QPointF to=GisGrid::worldToModel(grid.origin());

        if (fmod(to.x(), cRUSize) != 0. || fmod(to.y(), cRUSize) != 0.) {
            QPointF world_offset = GisGrid::modelToWorld(QPointF(0., 0.));
            throw IException(QString("Loading of the snapshot '%1' failed: The offset from the current location of the project (%4/%5) " \
                                     "is not a multiple of the resource unit size (100m) relative to grid of the snapshot (origin-x: %2, origin-y: %3).").arg(file_name)
                             .arg(grid.origin().x()).arg(grid.origin().y()).arg(world_offset.x()).arg(world_offset.y()));
        }


        const Grid<ResourceUnit*> &rugrid = GlobalSettings::instance()->model()->RUgrid();
        for (int i=0;i<rugrid.count();++i) {
            const ResourceUnit *ru = rugrid.constValueAtIndex(i);
            if (ru && ru->index()>-1) {
               int value = grid.value( rugrid.cellCenterPoint(i) );
               if (value>-1)
                   mRUHash[value] = const_cast<ResourceUnit*>(ru);
            }
        }

    }


    loadTrees();
    loadSoil();
    loadSnags();
    // load saplings only when regeneration is enabled (this can save a lot of time)
    if (GlobalSettings::instance()->model()->settings().regenerationEnabled) {
        loadSaplings();
        //loadSaplingsOld();
    }
    QSqlDatabase::database("snapshot").close();

    // after changing the trees, do a complete apply/read pattern cycle over the landscape...
    GlobalSettings::instance()->model()->onlyApplyLightPattern();
    qDebug() << "applied light pattern...";

    // refresh the stand statistics
    foreach (ResourceUnit *ru, GlobalSettings::instance()->model()->ruList()) {
         ru->recreateStandStatistics(true); // true: recalculate statistics
     }

    qDebug() << "created stand statistics...";
    qDebug() << "loading of snapshot completed.";

    return true;
}

bool Snapshot::saveStandSnapshot(const int stand_id, const MapGrid *stand_grid, const QString &file_name)
{
    // Check database
    QSqlDatabase db=QSqlDatabase::database("snapshotstand");
    if (!db.isOpen()) {
        openStandDatabase(GlobalSettings::instance()->path(file_name), false);
        db=QSqlDatabase::database("snapshotstand");
        // check if tree/sapling tables are already present
        if (!db.tables().contains("trees_stand") || !db.tables().contains("saplings_stand")) {
            // create tables
            QSqlQuery q(db);
            // trees
            q.exec("drop table trees_stand");
            q.exec("create table trees_stand (standID integer, ID integer, posX integer, posY integer, species text,  age integer, height real, dbh real, leafArea real, opacity real, foliageMass real, woodyMass real, fineRootMass real, coarseRootMass real, NPPReserve real, stressIndex real)");
            // saplings/regeneration
            q.exec("drop table saplings_stand");
            q.exec("create table saplings_stand (standID integer, posx integer, posy integer, species_index integer, age integer, height float, stress_years integer, flags integer)");

        }
    }
    // save trees
    QSqlQuery q(db);
    q.exec(QString("delete from trees_stand where standID=%1").arg(stand_id));

    if (!q.prepare(QString("insert into trees_stand (standID, ID, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex) " \
                      "values (:standid, :id, :x, :y, :spec, :age, :h, :d, :la, :opa, :mfol, :mwood, :mfr, :mcr, :npp, :si)")))
        throw IException(QString("Snapshot::saveTrees: prepare:") + q.lastError().text());

    db.transaction();
    QPointF offset = GisGrid::modelToWorld(QPointF(0.,0.));
    QList<Tree*> tree_list = stand_grid->trees(stand_id);
    QList<Tree*>::const_iterator it;
    for (it = tree_list.constBegin(); it!= tree_list.constEnd(); ++it)
    {
        Tree *t = *it;
        q.addBindValue(stand_id);
        q.addBindValue(t->id());
        q.addBindValue(t->position().x() + offset.x());
        q.addBindValue(t->position().y() + offset.y());
        q.addBindValue(t->species()->id());
        q.addBindValue(t->age());
        q.addBindValue(t->height());
        q.addBindValue(t->dbh());
        q.addBindValue(t->leafArea());
        q.addBindValue(t->mOpacity);
        q.addBindValue(t->biomassFoliage());
        q.addBindValue(t->biomassStem());
        q.addBindValue(t->biomassFineRoot());
        q.addBindValue(t->biomassCoarseRoot());
        q.addBindValue(t->mNPPReserve);
        q.addBindValue(t->mStressIndex);
        if (!q.exec()) {
            throw IException(QString("Snapshot::saveStandSnapshot, Trees: execute:") + q.lastError().text());
        }

    }
    // save saplings
    // loop over all pixels, only when regeneration is enabled
    if (GlobalSettings::instance()->model()->settings().regenerationEnabled) {
        q.exec(QString("delete from saplings_stand where standID=%1").arg(stand_id));

        if (!q.prepare(QString("insert into saplings_stand (standID, posx, posy, species_index, age, height, stress_years, flags) " \
                               "values (?,?,?,?,?,?,?,?)")))
            throw IException(QString("Snapshot::saveSaplings: prepare:") + q.lastError().text());


        SaplingCellRunner scr(stand_id, stand_grid);
        while (SaplingCell *sc = scr.next()) {
            for (int i=0;i<NSAPCELLS;++i)
                if (sc->saplings[i].is_occupied()) {
                    q.addBindValue(stand_id);
                    QPointF t = scr.currentCoord();
                    q.addBindValue(t.x() + offset.x());
                    q.addBindValue(t.y() + offset.y());
                    q.addBindValue(sc->saplings[i].species_index);
                    q.addBindValue(sc->saplings[i].age);
                    q.addBindValue(sc->saplings[i].height);
                    q.addBindValue(sc->saplings[i].stress_years);
                    q.addBindValue(sc->saplings[i].flags);
                    if (!q.exec()) {
                        throw IException(QString("Snapshot::saveStandSnapshot, saplings: execute:") + q.lastError().text());
                    }
                }
        }
    }

    db.commit();

    return true;
}

bool Snapshot::loadStandSnapshot(const int stand_id, const MapGrid *stand_grid, const QString &file_name)
{
    QSqlDatabase db=QSqlDatabase::database("snapshotstand");
    if (!db.isOpen()) {
        openStandDatabase(GlobalSettings::instance()->path(file_name), true);
        db=QSqlDatabase::database("snapshotstand");
    }
    // load trees
    // kill all living trees on the stand
    QList<Tree*> tree_list = stand_grid->trees(stand_id);
    QList<Tree*>::const_iterator it;
    int n_removed = tree_list.count();
    for (it = tree_list.constBegin(); it!= tree_list.constEnd(); ++it) {
        (*it)->remove(1., 1., 1.);
    }

    // load from database
    QSqlQuery q(db);
    q.setForwardOnly(true);
    q.exec(QString("select standID, ID, posX, posY, species,  age, height, dbh, leafArea, opacity, "
                   "foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex "
                   "from trees_stand where standID=%1").arg(stand_id));
    QRectF extent = GlobalSettings::instance()->model()->extent();
    int n=0, sap_n=0, n_sap_removed=0;
    while (q.next()) {
        QPointF coord(GisGrid::worldToModel(QPointF(q.value(2).toInt(), q.value(3).toInt())));
        if (!extent.contains(coord))
            continue;
        ResourceUnit *ru = GlobalSettings::instance()->model()->ru(coord);
        if (!ru)
            continue;
        Tree &t = ru->newTree();
        t.setRU(ru);
        t.mId = q.value(1).toInt();
        t.setPosition(coord);
        Species *s = GlobalSettings::instance()->model()->speciesSet()->species(q.value(4).toString());
        if (!s)
            throw IException("Snapshot::loadTrees: Invalid species");
        t.setSpecies(s);
        t.mAge = q.value(5).toInt();
        t.mHeight = q.value(6).toFloat();
        t.mDbh = q.value(7).toFloat();
        t.mLeafArea = q.value(8).toFloat();
        t.mOpacity = q.value(9).toFloat();
        t.mFoliageMass = q.value(10).toFloat();
        t.mWoodyMass = q.value(11).toFloat();
        t.mFineRootMass = q.value(12).toFloat();
        t.mCoarseRootMass = q.value(13).toFloat();
        t.mNPPReserve = q.value(14).toFloat();
        t.mStressIndex = q.value(15).toFloat();
        t.mStamp = s->stamp(t.mDbh, t.mHeight);
        n++;
    }

    // now the saplings
    if (GlobalSettings::instance()->model()->settings().regenerationEnabled) {
        // (1) remove all saplings:
        SaplingCellRunner scr(stand_id, stand_grid);
        while (SaplingCell *sc = scr.next()) {
            n_sap_removed += sc->n_occupied();
            GlobalSettings::instance()->model()->saplings()->clearSaplings(sc, scr.ru(),true);
        }

        // (2) load saplings from database
        q.exec(QString("select posx, posy, species_index, age, height, stress_years, flags "
                       "from saplings_stand where standID=%1").arg(stand_id));
        while (q.next()) {
            QPointF coord(GisGrid::worldToModel(QPointF(q.value(0).toInt(), q.value(1).toInt())));
            if (!extent.contains(coord))
                continue;
            SaplingCell *sc = GlobalSettings::instance()->model()->saplings()->cell(GlobalSettings::instance()->model()->grid()->indexAt(coord));
            if (!sc)
                continue;
            if (SaplingTree *st = sc->addSapling(q.value(4).toFloat(),
                                                 q.value(3).toInt(),
                                                 q.value(2).toInt())) {
                st->stress_years = static_cast<unsigned char>(q.value(5).toChar().toLatin1());
                st->flags = static_cast<unsigned char>(q.value(6).toChar().toLatin1());
            }
            sap_n++;

        }

    }
    // clean up
    GlobalSettings::instance()->model()->cleanTreeLists(true);

    qDebug() << "load stand snapshot for stand "<< stand_id << ": trees (removed/loaded): " <<n_removed<<"/" << n <<", saplings (removed/loaded):" << n_sap_removed << "/" << sap_n;

    return true;
}

void Snapshot::saveTrees()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    AllTreeIterator at(GlobalSettings::instance()->model());
    QSqlQuery q(db);
    if (!q.prepare(QString("insert into trees (ID, RUindex, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex) " \
                      "values (:id, :index, :x, :y, :spec, :age, :h, :d, :la, :opa, :mfol, :mwood, :mfr, :mcr, :npp, :si)")))
        throw IException(QString("Snapshot::saveTrees: prepare:") + q.lastError().text());

    int n = 0;
    db.transaction();
    while (Tree *t = at.next()) {
        q.addBindValue(t->id());
        q.addBindValue(t->ru()->index());
        q.addBindValue(t->mPositionIndex.x());
        q.addBindValue(t->mPositionIndex.y());
        q.addBindValue(t->species()->id());
        q.addBindValue(t->age());
        q.addBindValue(t->height());
        q.addBindValue(t->dbh());
        q.addBindValue(t->leafArea());
        q.addBindValue(t->mOpacity);
        q.addBindValue(t->biomassFoliage());
        q.addBindValue(t->biomassStem());
        q.addBindValue(t->biomassFineRoot());
        q.addBindValue(t->biomassCoarseRoot());
        q.addBindValue(t->mNPPReserve);
        q.addBindValue(t->mStressIndex);
        if (!q.exec()) {
            throw IException(QString("Snapshot::saveTrees: execute:") + q.lastError().text());
        }
        if (++n % 10000 == 0) {
            qDebug() << n << "trees saved...";
            QCoreApplication::processEvents();
        }
    }
    db.commit();
    qDebug() << "Snapshot: finished trees. N=" << n;
}

void Snapshot::loadTrees()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    // setForwardOnly() -> helps avoiding that the query caches all the data
    // during iterating
    q.setForwardOnly(true);
    q.exec("select ID, RUindex, posX, posY, species,  age, height, dbh, leafArea, opacity, foliageMass, woodyMass, fineRootMass, coarseRootMass, NPPReserve, stressIndex from trees");
    int ru_index = -1;
    int new_ru;
    int offsetx=0, offsety=0;
    ResourceUnit *ru = 0;
    int n=0, ntotal=0;
    try {
        // clear all trees on the landscape
        foreach (ResourceUnit *ru, GlobalSettings::instance()->model()->ruList())
            ru->trees().clear();
        // load the trees from the database
        while (q.next()) {
            new_ru = q.value(1).toInt();
            ++ntotal;
            if (new_ru != ru_index) {
                ru_index = new_ru;
                ru = mRUHash[ru_index];
                if (ru) {
                    offsetx = ru->cornerPointOffset().x();
                    offsety = ru->cornerPointOffset().y();
                }

            }
            if (!ru)
                continue;
            // add a new tree to the tree list
            //ru->trees().append(Tree());
            //Tree &t = ru->trees().back();
            Tree &t = ru->newTree();
            t.setRU(ru);
            t.mId = q.value(0).toInt();
            t.mPositionIndex.setX(offsetx + q.value(2).toInt() % cPxPerRU);
            t.mPositionIndex.setY(offsety + q.value(3).toInt() % cPxPerRU);
            Species *s = GlobalSettings::instance()->model()->speciesSet()->species(q.value(4).toString());
            if (!s)
                throw IException("Snapshot::loadTrees: Invalid species");
            t.setSpecies(s);
            t.mAge = q.value(5).toInt();
            t.mHeight = q.value(6).toFloat();
            t.mDbh = q.value(7).toFloat();
            t.mLeafArea = q.value(8).toFloat();
            t.mOpacity = q.value(9).toFloat();
            t.mFoliageMass = q.value(10).toFloat();
            t.mWoodyMass = q.value(11).toFloat();
            t.mFineRootMass = q.value(12).toFloat();
            t.mCoarseRootMass = q.value(13).toFloat();
            t.mNPPReserve = q.value(14).toFloat();
            t.mStressIndex = q.value(15).toFloat();
            t.mStamp = s->stamp(t.mDbh, t.mHeight);


            if (n<10000000 && ++n % 10000 == 0) {
                qDebug() << n << "trees loaded...";
                QCoreApplication::processEvents();
            }

        }
    } catch (const std::bad_alloc &) {
        throw IException(QString("bad_alloc exception after %1 trees!!!!").arg(n));
    }


    qDebug() << "Snapshot: finished trees. N=" << n << "from trees in snapshot:" << ntotal;
}


void Snapshot::saveSoil()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    if (!q.prepare(QString("insert into soil (RUindex, kyl, kyr, inLabC, inLabN, inLabP, inRefC, inRefN, inRefP, YLC, YLN, YLP, YRC, YRN, YRP, SOMC, SOMN, WaterContent, SnowPack) " \
                      "values (:idx, :kyl, :kyr, :inLabC, :iLN, :iLP, :iRC, :iRN, :iRP, :ylc, :yln, :ylp, :yrc, :yrn, :yrp, :somc, :somn, :wc, :snowpack)")))
        throw IException(QString("Snapshot::saveSoil: prepare:") + q.lastError().text());

    int n = 0;
    db.transaction();
    foreach (ResourceUnit *ru, GlobalSettings::instance()->model()->ruList()) {
        Soil *s = ru->soil();
        if (s) {
            q.addBindValue(s->mRU->index());
            q.addBindValue(s->mKyl);
            q.addBindValue(s->mKyr);
            q.addBindValue(s->mInputLab.C);
            q.addBindValue(s->mInputLab.N);
            q.addBindValue(s->mInputLab.parameter());
            q.addBindValue(s->mInputRef.C);
            q.addBindValue(s->mInputRef.N);
            q.addBindValue(s->mInputRef.parameter());
            q.addBindValue(s->mYL.C);
            q.addBindValue(s->mYL.N);
            q.addBindValue(s->mYL.parameter());
            q.addBindValue(s->mYR.C);
            q.addBindValue(s->mYR.N);
            q.addBindValue(s->mYR.parameter());
            q.addBindValue(s->mSOM.C);
            q.addBindValue(s->mSOM.N);
            q.addBindValue(ru->waterCycle()->currentContent());
            q.addBindValue(ru->waterCycle()->currentSnowPack());
            if (!q.exec()) {
                throw IException(QString("Snapshot::saveSoil: execute:") + q.lastError().text());
            }
            if (++n % 1000 == 0) {
                qDebug() << n << "soil resource units saved...";
                QCoreApplication::processEvents();
            }
        }
    }

    db.commit();
    qDebug() << "Snapshot: finished Soil. N=" << n;
}

void Snapshot::loadSoil()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    q.exec("select RUindex, kyl, kyr, inLabC, inLabN, inLabP, inRefC, inRefN, inRefP, YLC, YLN, YLP, YRC, YRN, YRP, SOMC, SOMN, WaterContent, SnowPack from soil");
    int ru_index = -1;
    ResourceUnit *ru = 0;
    int n=0;
    while (q.next()) {
        ru_index = q.value(0).toInt();
        ru = mRUHash[ru_index];
        if (!ru)
            continue;
        Soil *s = ru->soil();
        if (!s) {
            throw IException("Snapshot::loadSoil: trying to load soil data but soil module is disabled.");
        }
        s->mKyl = q.value(1).toDouble();
        s->mKyr = q.value(2).toDouble();
        s->mInputLab.C = q.value(3).toDouble();
        s->mInputLab.N = q.value(4).toDouble();
        s->mInputLab.setParameter( q.value(5).toDouble());
        s->mInputRef.C = q.value(6).toDouble();
        s->mInputRef.N = q.value(7).toDouble();
        s->mInputRef.setParameter( q.value(8).toDouble());
        s->mYL.C = q.value(9).toDouble();
        s->mYL.N = q.value(10).toDouble();
        s->mYL.setParameter( q.value(11).toDouble());
        s->mYR.C = q.value(12).toDouble();
        s->mYR.N = q.value(13).toDouble();
        s->mYR.setParameter( q.value(14).toDouble());
        s->mSOM.C = q.value(15).toDouble();
        s->mSOM.N = q.value(16).toDouble();
        const_cast<WaterCycle*>(ru->waterCycle())->setContent(q.value(17).toDouble(), q.value(18).toDouble());

        if (++n % 1000 == 0) {
            qDebug() << n << "soil units loaded...";
            QCoreApplication::processEvents();
        }
    }
    qDebug() << "Snapshot: finished soil. N=" << n;

}


void Snapshot::saveSnags()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    if (!q.prepare(QString("insert into snag(RUIndex, climateFactor, SWD1C, SWD1N, SWD2C, SWD2N, SWD3C, SWD3N, " \
                           "totalSWDC, totalSWDN, NSnags1, NSnags2, NSnags3, dbh1, dbh2, dbh3, height1, height2, height3, " \
                           "volume1, volume2, volume3, tsd1, tsd2, tsd3, ksw1, ksw2, ksw3, halflife1, halflife2, halflife3, " \
                           "branch1C, branch1N, branch2C, branch2N, branch3C, branch3N, branch4C, branch4N, branch5C, branch5N, branchIndex) " \
                          "values (?,?,?,?,?,?,?,?, " \
                           "?,?,?,?,?,?,?,?,?,?,?," \
                           "?,?,?,?,?,?,?,?,?,?,?,?," \
                           "?,?,?,?,?,?,?,?,?,?,?)")))
        throw IException(QString("Snapshot::saveSnag: prepare:") + q.lastError().text());

    int n = 0;
    db.transaction();
    foreach (ResourceUnit *ru, GlobalSettings::instance()->model()->ruList()) {
        Snag *s = ru->snag();
        if (!s)
            continue;
        q.addBindValue(s->mRU->index());
        q.addBindValue(s->mClimateFactor);
        q.addBindValue(s->mSWD[0].C);
        q.addBindValue(s->mSWD[0].N);
        q.addBindValue(s->mSWD[1].C);
        q.addBindValue(s->mSWD[1].N);
        q.addBindValue(s->mSWD[2].C);
        q.addBindValue(s->mSWD[2].N);
        q.addBindValue(s->mTotalSWD.C);
        q.addBindValue(s->mTotalSWD.N);
        q.addBindValue(s->mNumberOfSnags[0]);
        q.addBindValue(s->mNumberOfSnags[1]);
        q.addBindValue(s->mNumberOfSnags[2]);
        q.addBindValue(s->mAvgDbh[0]);
        q.addBindValue(s->mAvgDbh[1]);
        q.addBindValue(s->mAvgDbh[2]);
        q.addBindValue(s->mAvgHeight[0]);
        q.addBindValue(s->mAvgHeight[1]);
        q.addBindValue(s->mAvgHeight[2]);
        q.addBindValue(s->mAvgVolume[0]);
        q.addBindValue(s->mAvgVolume[1]);
        q.addBindValue(s->mAvgVolume[2]);
        q.addBindValue(s->mTimeSinceDeath[0]);
        q.addBindValue(s->mTimeSinceDeath[1]);
        q.addBindValue(s->mTimeSinceDeath[2]);
        q.addBindValue(s->mKSW[0]);
        q.addBindValue(s->mKSW[1]);
        q.addBindValue(s->mKSW[2]);
        q.addBindValue(s->mHalfLife[0]);
        q.addBindValue(s->mHalfLife[1]);
        q.addBindValue(s->mHalfLife[2]);
        q.addBindValue(s->mOtherWood[0].C); q.addBindValue(s->mOtherWood[0].N);
        q.addBindValue(s->mOtherWood[1].C); q.addBindValue(s->mOtherWood[1].N);
        q.addBindValue(s->mOtherWood[2].C); q.addBindValue(s->mOtherWood[2].N);
        q.addBindValue(s->mOtherWood[3].C); q.addBindValue(s->mOtherWood[3].N);
        q.addBindValue(s->mOtherWood[4].C); q.addBindValue(s->mOtherWood[4].N);
        q.addBindValue(s->mBranchCounter);

        if (!q.exec()) {
            throw IException(QString("Snapshot::saveSnag: execute:") + q.lastError().text());
        }
        if (++n % 1000 == 0) {
            qDebug() << n << "snags saved...";
            QCoreApplication::processEvents();
        }
    }

    db.commit();
    qDebug() << "Snapshot: finished Snags. N=" << n;
}

void Snapshot::loadSnags()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    q.exec("select RUIndex, climateFactor, SWD1C, SWD1N, SWD2C, SWD2N, SWD3C, SWD3N, totalSWDC, totalSWDN, NSnags1, NSnags2, NSnags3, dbh1, dbh2, dbh3, height1, height2, height3, volume1, volume2, volume3, tsd1, tsd2, tsd3, ksw1, ksw2, ksw3, halflife1, halflife2, halflife3, branch1C, branch1N, branch2C, branch2N, branch3C, branch3N, branch4C, branch4N, branch5C, branch5N, branchIndex from snag");
    int ru_index = -1;
    ResourceUnit *ru = 0;
    int n=0;
    int ci=0;
    while (q.next()) {
        ru_index = q.value(ci++).toInt();
        ru = mRUHash[ru_index];
        if (!ru)
            continue;
        Snag *s = ru->snag();
        if (!s)
            continue;
        s->mClimateFactor = q.value(ci++).toDouble();
        s->mSWD[0].C = q.value(ci++).toDouble();
        s->mSWD[0].N = q.value(ci++).toDouble();
        s->mSWD[1].C = q.value(ci++).toDouble();
        s->mSWD[1].N = q.value(ci++).toDouble();
        s->mSWD[2].C = q.value(ci++).toDouble();
        s->mSWD[2].N = q.value(ci++).toDouble();
        s->mTotalSWD.C = q.value(ci++).toDouble();
        s->mTotalSWD.N = q.value(ci++).toDouble();
        s->mNumberOfSnags[0] = q.value(ci++).toDouble();
        s->mNumberOfSnags[1] = q.value(ci++).toDouble();
        s->mNumberOfSnags[2] = q.value(ci++).toDouble();
        s->mAvgDbh[0] = q.value(ci++).toDouble();
        s->mAvgDbh[1] = q.value(ci++).toDouble();
        s->mAvgDbh[2] = q.value(ci++).toDouble();
        s->mAvgHeight[0] = q.value(ci++).toDouble();
        s->mAvgHeight[1] = q.value(ci++).toDouble();
        s->mAvgHeight[2] = q.value(ci++).toDouble();
        s->mAvgVolume[0] = q.value(ci++).toDouble();
        s->mAvgVolume[1] = q.value(ci++).toDouble();
        s->mAvgVolume[2] = q.value(ci++).toDouble();
        s->mTimeSinceDeath[0] = q.value(ci++).toDouble();
        s->mTimeSinceDeath[1] = q.value(ci++).toDouble();
        s->mTimeSinceDeath[2] = q.value(ci++).toDouble();
        s->mKSW[0] = q.value(ci++).toDouble();
        s->mKSW[1] = q.value(ci++).toDouble();
        s->mKSW[2] = q.value(ci++).toDouble();
        s->mHalfLife[0] = q.value(ci++).toDouble();
        s->mHalfLife[1] = q.value(ci++).toDouble();
        s->mHalfLife[2] = q.value(ci++).toDouble();
        s->mOtherWood[0].C = q.value(ci++).toDouble(); s->mOtherWood[0].N = q.value(ci++).toDouble();
        s->mOtherWood[1].C = q.value(ci++).toDouble(); s->mOtherWood[1].N = q.value(ci++).toDouble();
        s->mOtherWood[2].C = q.value(ci++).toDouble(); s->mOtherWood[2].N = q.value(ci++).toDouble();
        s->mOtherWood[3].C = q.value(ci++).toDouble(); s->mOtherWood[3].N = q.value(ci++).toDouble();
        s->mOtherWood[4].C = q.value(ci++).toDouble(); s->mOtherWood[4].N = q.value(ci++).toDouble();
        s->mBranchCounter = q.value(ci++).toInt();

        if (++n % 1000 == 0) {
            qDebug() << n << "snags loaded...";
            QCoreApplication::processEvents();
        }
    }
    qDebug() << "Snapshot: finished snags. N=" << n;

}

void Snapshot::saveSaplings()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    if (!q.prepare(QString("insert into saplings (RUindex, species, posx, posy, age, height, stress_years) " \
                           "values (?,?,?,?,?,?,?)")))
        throw IException(QString("Snapshot::saveSaplings: prepare:") + q.lastError().text());

    int n = 0;
    db.transaction();
    throw IException("Snapshot::saveSaplings() not implemented");
//    foreach (const ResourceUnit *ru, GlobalSettings::instance()->model()->ruList()) {
//        foreach (const ResourceUnitSpecies *rus, ru->ruSpecies()) {
//            const Sapling &sap = rus->sapling();
//            if (sap.saplings().isEmpty())
//                continue;
//            foreach (const SaplingTreeOld &t, sap.saplings()) {
//                if (!t.pixel)
//                    continue;
//                q.addBindValue(ru->index());
//                q.addBindValue(rus->species()->id());
//                QPoint p=t.coords();
//                q.addBindValue(p.x());
//                q.addBindValue(p.y());
//                q.addBindValue(t.age.age);
//                q.addBindValue(t.height);
//                q.addBindValue(t.age.stress_years);
//                if (!q.exec()) {
//                    throw IException(QString("Snapshot::saveSaplings: execute:") + q.lastError().text());
//                }
//                if (++n % 10000 == 0) {
//                    qDebug() << n << "saplings saved...";
//                    QCoreApplication::processEvents();
//                }
//            }
//        }
//    }
    db.commit();
    qDebug() << "Snapshot: finished saplings. N=" << n;
}

void Snapshot::loadSaplings()
{
    QSqlDatabase db=QSqlDatabase::database("snapshot");
    QSqlQuery q(db);
    q.setForwardOnly(true); // avoid huge memory usage in query component
    if (!q.exec("select RUindex, species, posx, posy, age, height, stress_years from saplings")) {
        qDebug() << "Error when loading from saplings table...." << q.lastError().text();
        return;
    }
    int ru_index = -1;

    // clear all saplings in the whole project area: added for testing/debugging
//    foreach( ResourceUnit *ru, GlobalSettings::instance()->model()->ruList()) {
//        foreach (ResourceUnitSpecies *rus, ru->ruSpecies()) {
//            rus->changeSapling().clear();
//            rus->changeSapling().clearStatistics();
//        }
//    }

    ResourceUnit *ru = 0;
    int n=0, ntotal=0;
    int ci;
    int posx, posy;
    int offsetx=0, offsety=0;
    Saplings *saplings = GlobalSettings::instance()->model()->saplings();

    while (q.next()) {
        ci = 0;
        ru_index = q.value(ci++).toInt();
        ru = mRUHash[ru_index];
        if (!ru)
            continue;
        Species *species = ru->speciesSet()->species(q.value(ci++).toString());
        if (!species)
            throw IException("Snapshot::loadSaplings: Invalid species");

        offsetx = ru->cornerPointOffset().x();
        offsety = ru->cornerPointOffset().y();

        posx = offsetx + q.value(ci++).toInt() % cPxPerRU;
        posy = offsety + q.value(ci++).toInt() % cPxPerRU;

        SaplingCell *sc = saplings->cell(QPoint(posx, posy));
        if (!sc)
            continue;

        int age=q.value(ci++).toInt();
        SaplingTree *st = sc->addSapling(q.value(ci++).toFloat(), age, species->index());
        if (!st)
            continue;
        st->stress_years = static_cast<unsigned char> (q.value(ci++).toInt());
        ++ntotal;


         if (n<10000000 && ++n % 10000 == 0) {
            qDebug() << n << "saplings loaded...";
            QCoreApplication::processEvents();
        }
        if (n>=10000000 && ++n % 1000000 == 0) {
            qDebug() << n << "saplings loaded...";
            QCoreApplication::processEvents();
        }


    }
    qDebug() << "Snapshot: finished loading saplings. N=" << n << "from N in snapshot:" << ntotal;

}


void Snapshot::loadSaplingsOld()
{
//    QSqlDatabase db=QSqlDatabase::database("snapshot");
//    QSqlQuery q(db);
//    q.setForwardOnly(true); // avoid huge memory usage in query component
//    if (!q.exec("select RUindex, species, posx, posy, age, height, stress_years from saplings")) {
//        qDebug() << "Error when loading from saplings table...." << q.lastError().text();
//        return;
//    }
//    int ru_index = -1;

//    // clear all saplings in the whole project area: added for testing/debugging
////    foreach( ResourceUnit *ru, GlobalSettings::instance()->model()->ruList()) {
////        foreach (ResourceUnitSpecies *rus, ru->ruSpecies()) {
////            rus->changeSapling().clear();
////            rus->changeSapling().clearStatistics();
////        }
////    }

//    ResourceUnit *ru = 0;
//    int n=0, ntotal=0;
//    int ci;
//    int posx, posy;
//    int offsetx=0, offsety=0;
//    Sapling *last_sapling = 0;

//    while (q.next()) {
//        ci = 0;
//        ru_index = q.value(ci++).toInt();
//        ++ntotal;
//        ru = mRUHash[ru_index];
//        if (!ru)
//            continue;
//        Species *species = ru->speciesSet()->species(q.value(ci++).toString());
//        if (!species)
//            throw IException("Snapshot::loadSaplings: Invalid species");
//        Sapling &sap = ru->resourceUnitSpecies(species).changeSapling();
//        if (last_sapling != &sap) {
//            last_sapling = &sap;
//            sap.clear(); // clears the trees and the bitmap
//            sap.clearStatistics();
//            offsetx = ru->cornerPointOffset().x();
//            offsety = ru->cornerPointOffset().y();
//        }
//        sap.mSaplingTrees.push_back(SaplingTreeOld());
//        SaplingTreeOld &t = sap.mSaplingTrees.back();
//        //posx = q.value(ci++).toInt();
//        //posy = q.value(ci++).toInt();
//        posx = offsetx + q.value(ci++).toInt() % cPxPerRU;
//        posy = offsety + q.value(ci++).toInt() % cPxPerRU;
//        if (GlobalSettings::instance()->model()->grid()->isIndexValid(posx, posy)) {
//            t.pixel = GlobalSettings::instance()->model()->grid()->ptr(posx,posy );
//        } else {
//            continue;
//        }
//        t.age.age = static_cast<short unsigned int>( q.value(ci++).toInt() );
//        t.height = q.value(ci++).toFloat();
//        t.age.stress_years = static_cast<short unsigned int> (q.value(ci++).toInt() );
//        sap.setBit(QPoint(posx, posy), true); // set the flag in the bitmap
//        if (n<10000000 && ++n % 10000 == 0) {
//            qDebug() << n << "saplings loaded...";
//            QCoreApplication::processEvents();
//        }
//        if (n>=10000000 && ++n % 1000000 == 0) {
//            qDebug() << n << "saplings loaded...";
//            QCoreApplication::processEvents();
//        }


//    }
//    qDebug() << "Snapshot: finished loading saplings. N=" << n << "from N in snapshot:" << ntotal;

}


